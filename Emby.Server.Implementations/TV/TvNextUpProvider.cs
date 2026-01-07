#pragma warning disable CS1591

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.TV
{
    public sealed class TvNextUpProvider : INextUpProvider
    {
        private readonly ITVSeriesManager _tvSeriesManager;

        public TvNextUpProvider(ITVSeriesManager tvSeriesManager)
        {
            _tvSeriesManager = tvSeriesManager;
        }

        public int Priority => 0;

        public bool CanHandle(NextUpQuery query, IReadOnlyCollection<BaseItem>? parentFolders)
            => true;

        public Task<QueryResult<BaseItem>> GetNextUpAsync(
            NextUpQuery query,
            IReadOnlyCollection<BaseItem>? parentFolders,
            DtoOptions options,
            CancellationToken cancellationToken)
        {
            if (parentFolders is null)
            {
                return Task.FromResult(_tvSeriesManager.GetNextUp(query, options));
            }

            var parentArray = parentFolders as BaseItem[] ?? parentFolders.ToArray();
            return Task.FromResult(_tvSeriesManager.GetNextUp(query, parentArray, options));
        }
    }
}
