#pragma warning disable CS1591

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.TV
{
    public interface INextUpProvider
    {
        int Priority { get; }

        bool CanHandle(NextUpQuery query, IReadOnlyCollection<BaseItem>? parentFolders);

        Task<QueryResult<BaseItem>> GetNextUpAsync(NextUpQuery query, IReadOnlyCollection<BaseItem>? parentFolders, DtoOptions options, CancellationToken cancellationToken);
    }
}
