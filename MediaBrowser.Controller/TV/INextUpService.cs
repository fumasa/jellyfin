#pragma warning disable CS1591

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.TV
{
    public interface INextUpService
    {
        Task<QueryResult<BaseItem>> GetNextUpAsync(
            NextUpQuery query,
            DtoOptions options,
            CancellationToken cancellationToken,
            IReadOnlyCollection<BaseItem>? parentFolders = null);
    }
}
