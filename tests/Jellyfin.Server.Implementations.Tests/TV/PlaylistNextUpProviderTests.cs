using System;
using System.Collections.Generic;
using Emby.Server.Implementations.TV;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.TV
{
    public class PlaylistNextUpProviderTests
    {
        [Fact]
        public void GetNextUpFromPlaylist_ReturnsNextUnplayedItem()
        {
            var lastPlayed = DateTime.UtcNow.AddDays(-1);
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(lastPlayed),
                [episode2.Id] = Unplayed(),
                [episode3.Id] = Unplayed()
            };

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                CreateQuery());

            Assert.NotNull(result);
            Assert.Equal(episode2, result!.Item);
            Assert.Equal(lastPlayed, result.LastPlayedDate);
        }

        [Fact]
        public void GetNextUpFromPlaylist_ReturnsNullWhenNoPlayedItems()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Unplayed(),
                [episode2.Id] = Unplayed()
            };

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                CreateQuery());

            Assert.Null(result);
        }

        [Fact]
        public void GetNextUpFromPlaylist_RespectsResumableFlag()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(DateTime.UtcNow),
                [episode2.Id] = Resumable()
            };

            var query = CreateQuery();
            query.EnableResumable = false;

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.Null(result);
        }

        [Fact]
        public void GetNextUpFromPlaylist_SkipsResumableItemWhenDisabled()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(DateTime.UtcNow),
                [episode2.Id] = Resumable(),
                [episode3.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.EnableResumable = false;

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.NotNull(result);
            Assert.Equal(episode3, result!.Item);
        }

        [Fact]
        public void GetNextUpFromPlaylist_RespectsDateCutoff()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(DateTime.UtcNow.AddDays(-10)),
                [episode2.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.NextUpDateCutoff = DateTime.UtcNow.AddDays(-5);

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.Null(result);
        }

        [Fact]
        public void GetNextUpFromPlaylist_UsesMostRecentPlayedForCutoffWhenRewatchingEnabled()
        {
            var now = DateTime.UtcNow;
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(now),
                [episode2.Id] = Played(now.AddDays(-10)),
                [episode3.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.EnableRewatching = true;
            query.NextUpDateCutoff = now.AddDays(-5);

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.NotNull(result);
            Assert.Equal(episode2, result!.Item);
            Assert.Equal(now, result.LastPlayedDate);
        }

        [Fact]
        public void GetNextUpFromPlaylist_UsesAnchorPlayedDateForCutoffWhenRewatchingDisabled()
        {
            var now = DateTime.UtcNow;
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(now),
                [episode2.Id] = Played(now.AddDays(-10)),
                [episode3.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.EnableRewatching = false;
            query.NextUpDateCutoff = now.AddDays(-5);

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.Null(result);
        }

        [Fact]
        public void GetNextUpFromPlaylist_AllowsRewatching()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(DateTime.UtcNow),
                [episode2.Id] = Played(DateTime.UtcNow.AddDays(-1))
            };

            var query = CreateQuery();
            query.EnableRewatching = true;

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.NotNull(result);
            Assert.Equal(episode2, result!.Item);
        }

        [Fact]
        public void GetNextUpFromPlaylist_UsesMostRecentPlayedWhenRewatchingEnabled()
        {
            var now = DateTime.UtcNow;
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(now),
                [episode2.Id] = Played(now.AddDays(-1)),
                [episode3.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.EnableRewatching = true;

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.NotNull(result);
            Assert.Equal(episode2, result!.Item);
        }

        [Fact]
        public void GetNextUpFromPlaylist_UsesOrderWhenRewatchingDisabled()
        {
            var episode1 = CreateEpisode();
            var episode2 = CreateEpisode();
            var episode3 = CreateEpisode();

            var data = new Dictionary<Guid, UserItemData>
            {
                [episode1.Id] = Played(DateTime.UtcNow),
                [episode2.Id] = Played(DateTime.UtcNow.AddDays(-1)),
                [episode3.Id] = Unplayed()
            };

            var query = CreateQuery();
            query.EnableRewatching = false;

            var result = PlaylistNextUpProvider.GetNextUpFromPlaylist(
                new BaseItem[] { episode1, episode2, episode3 },
                item => data.TryGetValue(item.Id, out var userData) ? userData : null,
                query);

            Assert.NotNull(result);
            Assert.Equal(episode3, result!.Item);
        }

        [Fact]
        public void GetPlaylistOrderSettings_UsesOverrideWhenPresent()
        {
            var playlistId = Guid.NewGuid();
            var options = new NextUpPlaylistOptions
            {
                OrderBy = NextUpPlaylistOrderBy.PlaylistOrder,
                MetadataSortBy = ItemSortBy.DatePlayed,
                SortOrder = SortOrder.Descending,
                OrderOverrides = new[]
                {
                    new NextUpPlaylistOrderOverride
                    {
                        PlaylistId = playlistId,
                        OrderBy = NextUpPlaylistOrderBy.Metadata,
                        MetadataSortBy = ItemSortBy.SortName,
                        SortOrder = SortOrder.Ascending
                    }
                }
            };

            var settings = PlaylistNextUpProvider.GetPlaylistOrderSettings(options, playlistId);

            Assert.Equal(NextUpPlaylistOrderBy.Metadata, settings.OrderBy);
            Assert.Equal(ItemSortBy.SortName, settings.MetadataSortBy);
            Assert.Equal(SortOrder.Ascending, settings.SortOrder);
        }

        [Fact]
        public void GetPlaylistOrderSettings_UsesDefaultsWhenOverrideMissing()
        {
            var playlistId = Guid.NewGuid();
            var options = new NextUpPlaylistOptions
            {
                OrderBy = NextUpPlaylistOrderBy.PlaylistOrder,
                MetadataSortBy = ItemSortBy.DatePlayed,
                SortOrder = SortOrder.Descending
            };

            var settings = PlaylistNextUpProvider.GetPlaylistOrderSettings(options, playlistId);

            Assert.Equal(NextUpPlaylistOrderBy.PlaylistOrder, settings.OrderBy);
            Assert.Equal(ItemSortBy.DatePlayed, settings.MetadataSortBy);
            Assert.Equal(SortOrder.Descending, settings.SortOrder);
        }

        private static NextUpQuery CreateQuery()
        {
            return new NextUpQuery
            {
                User = new User("test", "auth", "reset")
            };
        }

        private static Episode CreateEpisode()
        {
            return new Episode
            {
                Id = Guid.NewGuid()
            };
        }

        private static UserItemData Played(DateTime date)
        {
            return new UserItemData
            {
                Key = "test",
                Played = true,
                LastPlayedDate = date
            };
        }

        private static UserItemData Unplayed()
        {
            return new UserItemData
            {
                Key = "test",
                Played = false
            };
        }

        private static UserItemData Resumable()
        {
            return new UserItemData
            {
                Key = "test",
                Played = false,
                PlaybackPositionTicks = 1
            };
        }
    }
}
