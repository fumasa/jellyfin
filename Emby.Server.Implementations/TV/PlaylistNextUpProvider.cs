#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.TV
{
    public sealed class PlaylistNextUpProvider : INextUpProvider
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ITVSeriesManager _tvSeriesManager;
        private readonly IServerConfigurationManager _configurationManager;

        public PlaylistNextUpProvider(
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            IUserDataManager userDataManager,
            ITVSeriesManager tvSeriesManager,
            IServerConfigurationManager configurationManager)
        {
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _userDataManager = userDataManager;
            _tvSeriesManager = tvSeriesManager;
            _configurationManager = configurationManager;
        }

        public int Priority => 10;

        public bool CanHandle(NextUpQuery query, IReadOnlyCollection<BaseItem>? parentFolders)
        {
            var options = _configurationManager.Configuration.NextUpPlaylistOptions;

            if (query.SeriesId.HasValue)
            {
                return false;
            }

            if (!options.UseAllPlaylists && (options.PlaylistIds is null || options.PlaylistIds.Length == 0))
            {
                return false;
            }

            return true;
        }

        public Task<QueryResult<BaseItem>> GetNextUpAsync(
            NextUpQuery query,
            IReadOnlyCollection<BaseItem>? parentFolders,
            DtoOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var playlistOptions = _configurationManager.Configuration.NextUpPlaylistOptions;
            var hidePlaylistItemsFromNextUp = playlistOptions.HidePlaylistItemsFromNextUp
                || playlistOptions.Mode == NextUpPlaylistMode.Replace;
            var playlistItemIds = hidePlaylistItemsFromNextUp ? new HashSet<Guid>() : null;
            var playlistItems = GetPlaylistNextUpItems(query, parentFolders, playlistOptions, cancellationToken, playlistItemIds, options);

            var tvQuery = CloneQueryWithoutPaging(query);
            var tvItems = GetTvNextUp(tvQuery, parentFolders, options).Items;
            if (playlistItemIds is { Count: > 0 })
            {
                tvItems = tvItems.Where(item => !playlistItemIds.Contains(item.Id)).ToArray();
            }

            var combined = MergeItems(tvItems, playlistItems);
            var totalCount = query.EnableTotalRecordCount ? combined.Count : 0;
            var paged = ApplyPaging(combined, query.StartIndex, query.Limit);

            return Task.FromResult(new QueryResult<BaseItem>(query.StartIndex, totalCount, paged));
        }

        private IReadOnlyList<BaseItem> GetPlaylistNextUpItems(
            NextUpQuery query,
            IReadOnlyCollection<BaseItem>? parentFolders,
            NextUpPlaylistOptions playlistOptions,
            CancellationToken cancellationToken,
            ISet<Guid>? playlistItemIds,
            DtoOptions options)
        {
            var playlists = GetPlaylists(query.User, playlistOptions);
            if (playlists.Count == 0)
            {
                return Array.Empty<BaseItem>();
            }

            var results = new List<PlaylistNextUpCandidate>(playlists.Count);
            var playlistOrder = 0;

            foreach (var playlist in playlists)
            {
                cancellationToken.ThrowIfCancellationRequested();
                playlistOrder++;

                var items = playlist.GetLinkedChildren(query.User);
                if (items.Count == 0)
                {
                    continue;
                }

                var filteredItems = FilterItems(items, query, parentFolders).ToList();
                if (filteredItems.Count == 0)
                {
                    continue;
                }

                if (playlistItemIds is not null)
                {
                    foreach (var item in filteredItems)
                    {
                        playlistItemIds.Add(item.Id);
                    }
                }

                var orderSettings = GetPlaylistOrderSettings(playlistOptions, playlist.Id);
                var orderedItems = OrderPlaylistItems(filteredItems, query.User, orderSettings);
                var nextUp = GetNextUpFromPlaylist(orderedItems, item => _userDataManager.GetUserData(query.User, item), query);
                if (nextUp is null)
                {
                    continue;
                }

                results.Add(new PlaylistNextUpCandidate(nextUp.Item, nextUp.LastPlayedDate, playlistOrder, playlist.Name));
            }

            var orderedCandidates = results
                .OrderByDescending(candidate => candidate.LastPlayedDate)
                .ThenBy(candidate => candidate.PlaylistOrder)
                .ToArray();

            if (orderedCandidates.Length > 0)
            {
                options.PlaylistNameByItemId ??= new Dictionary<Guid, string>();
                var playlistNameByItemId = options.PlaylistNameByItemId;
                if (playlistNameByItemId is not null)
                {
                    foreach (var candidate in orderedCandidates)
                    {
                        if (!string.IsNullOrWhiteSpace(candidate.PlaylistName))
                        {
                            playlistNameByItemId.TryAdd(candidate.Item.Id, candidate.PlaylistName);
                        }
                    }
                }
            }

            return orderedCandidates
                .Select(candidate => candidate.Item)
                .ToArray();
        }

        private IReadOnlyList<Playlist> GetPlaylists(User user, NextUpPlaylistOptions playlistOptions)
        {
            if (playlistOptions.UseAllPlaylists)
            {
                return _playlistManager.GetPlaylists(user.Id).ToList();
            }

            var playlists = new List<Playlist>();

            foreach (var playlistId in (playlistOptions.PlaylistIds ?? Array.Empty<Guid>()).Distinct())
            {
                var playlist = _playlistManager.GetPlaylistForUser(playlistId, user.Id);
                if (playlist is not null)
                {
                    playlists.Add(playlist);
                }
            }

            return playlists;
        }

        private static IEnumerable<BaseItem> FilterItems(
            IEnumerable<BaseItem> items,
            NextUpQuery query,
            IReadOnlyCollection<BaseItem>? parentFolders)
        {
            if (query.ParentId.HasValue)
            {
                var parentId = query.ParentId.Value;
                items = items.Where(item => item.Id.Equals(parentId) || item.GetAncestorIds().Contains(parentId));
            }

            if (parentFolders is not null && parentFolders.Count > 0)
            {
                var allowedTopParentIds = parentFolders.Select(folder => folder.Id).ToHashSet();
                items = items.Where(item =>
                {
                    var topParent = item.GetTopParent();
                    return topParent is not null && allowedTopParentIds.Contains(topParent.Id);
                });
            }

            return items;
        }

        internal static PlaylistOrderSettings GetPlaylistOrderSettings(
            NextUpPlaylistOptions playlistOptions,
            Guid playlistId)
        {
            var orderBy = playlistOptions.OrderBy;
            var metadataSortBy = playlistOptions.MetadataSortBy;
            var sortOrder = playlistOptions.SortOrder;

            var overrides = playlistOptions.OrderOverrides;
            if (overrides is not null)
            {
                var overrideOptions = overrides.FirstOrDefault(overrideOption => overrideOption.PlaylistId.Equals(playlistId));
                if (overrideOptions is not null)
                {
                    orderBy = overrideOptions.OrderBy;
                    metadataSortBy = overrideOptions.MetadataSortBy;
                    sortOrder = overrideOptions.SortOrder;
                }
            }

            return new PlaylistOrderSettings(orderBy, metadataSortBy, sortOrder);
        }

        private IReadOnlyList<BaseItem> OrderPlaylistItems(
            IReadOnlyList<BaseItem> items,
            User user,
            PlaylistOrderSettings orderSettings)
        {
            if (items.Count < 2)
            {
                return items;
            }

            switch (orderSettings.OrderBy)
            {
                case NextUpPlaylistOrderBy.Metadata:
                    if (orderSettings.MetadataSortBy == ItemSortBy.Default)
                    {
                        return items;
                    }

                    return _libraryManager
                        .Sort(items, user, new[] { orderSettings.MetadataSortBy }, orderSettings.SortOrder)
                        .ToArray();
                case NextUpPlaylistOrderBy.PlaylistOrder:
                default:
                    return items;
            }
        }

        private QueryResult<BaseItem> GetTvNextUp(
            NextUpQuery query,
            IReadOnlyCollection<BaseItem>? parentFolders,
            DtoOptions options)
        {
            if (parentFolders is null)
            {
                return _tvSeriesManager.GetNextUp(query, options);
            }

            var parentArray = parentFolders as BaseItem[] ?? parentFolders.ToArray();
            return _tvSeriesManager.GetNextUp(query, parentArray, options);
        }

        private static NextUpQuery CloneQueryWithoutPaging(NextUpQuery query)
        {
            return new NextUpQuery
            {
                User = query.User,
                ParentId = query.ParentId,
                SeriesId = query.SeriesId,
                StartIndex = null,
                Limit = null,
                EnableImageTypes = query.EnableImageTypes,
                EnableTotalRecordCount = query.EnableTotalRecordCount,
                NextUpDateCutoff = query.NextUpDateCutoff,
                EnableResumable = query.EnableResumable,
                EnableRewatching = query.EnableRewatching
            };
        }

        private static IReadOnlyList<BaseItem> MergeItems(
            IReadOnlyList<BaseItem> tvItems,
            IReadOnlyList<BaseItem> playlistItems)
        {
            if (tvItems.Count == 0)
            {
                return playlistItems;
            }

            if (playlistItems.Count == 0)
            {
                return tvItems;
            }

            var seen = new HashSet<Guid>();
            var combined = new List<BaseItem>(tvItems.Count + playlistItems.Count);

            AddItems(tvItems, combined, seen);
            AddItems(playlistItems, combined, seen);

            return combined;
        }

        private static void AddItems(
            IReadOnlyList<BaseItem> items,
            ICollection<BaseItem> target,
            ISet<Guid> seen)
        {
            foreach (var item in items)
            {
                if (seen.Add(item.Id))
                {
                    target.Add(item);
                }
            }
        }

        private static IReadOnlyList<BaseItem> ApplyPaging(
            IReadOnlyList<BaseItem> items,
            int? startIndex,
            int? limit)
        {
            IEnumerable<BaseItem> result = items;

            if (startIndex.HasValue)
            {
                result = result.Skip(startIndex.Value);
            }

            if (limit.HasValue && limit.Value > 0)
            {
                result = result.Take(limit.Value);
            }

            return result.ToArray();
        }

        internal static PlaylistNextUpResult? GetNextUpFromPlaylist(
            IReadOnlyList<BaseItem> items,
            Func<BaseItem, UserItemData?> getUserData,
            NextUpQuery query)
        {
            var lastPlayedIndex = -1;
            var lastPlayedDate = DateTime.MinValue;
            var mostRecentPlayedIndex = -1;
            var mostRecentPlayedDate = DateTime.MinValue;

            for (var i = 0; i < items.Count; i++)
            {
                var data = getUserData(items[i]);
                if (data?.Played != true || !data.LastPlayedDate.HasValue)
                {
                    continue;
                }

                var playedDate = data.LastPlayedDate.Value;
                lastPlayedIndex = i;
                lastPlayedDate = playedDate;
                if (playedDate >= mostRecentPlayedDate)
                {
                    mostRecentPlayedDate = playedDate;
                    mostRecentPlayedIndex = i;
                }
            }

            if (lastPlayedIndex < 0 || mostRecentPlayedIndex < 0)
            {
                return null;
            }

            var anchorIndex = query.EnableRewatching ? mostRecentPlayedIndex : lastPlayedIndex;
            var anchorDate = query.EnableRewatching ? mostRecentPlayedDate : lastPlayedDate;

            if (anchorDate < query.NextUpDateCutoff)
            {
                return null;
            }

            for (var i = anchorIndex + 1; i < items.Count; i++)
            {
                var item = items[i];
                var userData = getUserData(item);

                if (!query.EnableRewatching && userData?.Played == true)
                {
                    continue;
                }

                if (!query.EnableResumable && userData?.PlaybackPositionTicks > 0)
                {
                    continue;
                }

                return new PlaylistNextUpResult(item, anchorDate);
            }

            return null;
        }

        internal sealed record PlaylistOrderSettings(
            NextUpPlaylistOrderBy OrderBy,
            ItemSortBy MetadataSortBy,
            SortOrder SortOrder);

        internal sealed record PlaylistNextUpResult(BaseItem Item, DateTime LastPlayedDate);

        private sealed record PlaylistNextUpCandidate(BaseItem Item, DateTime LastPlayedDate, int PlaylistOrder, string PlaylistName);
    }
}
