#pragma warning disable CS1591

using System;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;

namespace MediaBrowser.Model.Configuration;

public class NextUpPlaylistOrderOverride
{
    public Guid PlaylistId { get; set; }

    public NextUpPlaylistOrderBy OrderBy { get; set; } = NextUpPlaylistOrderBy.PlaylistOrder;

    public ItemSortBy MetadataSortBy { get; set; } = ItemSortBy.SortName;

    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
}
