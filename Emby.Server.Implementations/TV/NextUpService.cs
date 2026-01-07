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
    public class NextUpService : INextUpService
    {
        private readonly INextUpProvider[] _providers;

        public NextUpService(IEnumerable<INextUpProvider> providers)
        {
            _providers = providers
                .OrderByDescending(provider => provider.Priority)
                .ToArray();
        }

        public Task<QueryResult<BaseItem>> GetNextUpAsync(
            NextUpQuery query,
            DtoOptions options,
            CancellationToken cancellationToken,
            IReadOnlyCollection<BaseItem>? parentFolders = null)
        {
            foreach (var provider in _providers)
            {
                if (provider.CanHandle(query, parentFolders))
                {
                    return provider.GetNextUpAsync(query, parentFolders, options, cancellationToken);
                }
            }

            return Task.FromResult(new QueryResult<BaseItem>());
        }
    }
}
