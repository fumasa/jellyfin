#pragma warning disable CS1591

using System;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;

namespace MediaBrowser.Model.Configuration;

public class NextUpPlaylistOptions
{
    public NextUpPlaylistMode Mode { get; set; } = NextUpPlaylistMode.Add;

    public bool HidePlaylistItemsFromNextUp { get; set; } = false;

    public bool UseAllPlaylists { get; set; } = false;

    public Guid[] PlaylistIds { get; set; } = Array.Empty<Guid>();

    public NextUpPlaylistOrderBy OrderBy { get; set; } = NextUpPlaylistOrderBy.PlaylistOrder;

    public ItemSortBy MetadataSortBy { get; set; } = ItemSortBy.SortName;

    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;

    public NextUpPlaylistOrderOverride[] OrderOverrides { get; set; } = Array.Empty<NextUpPlaylistOrderOverride>();
}
