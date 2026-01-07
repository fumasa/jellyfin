using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.TV;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Querying;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.TV
{
    public class NextUpServiceTests
    {
        [Fact]
        public async Task GetNextUpAsync_UsesHighestPriorityProvider()
        {
            var query = CreateQuery();
            var options = new DtoOptions();

            var lowProvider = new TestNextUpProvider(0, true, new QueryResult<BaseItem> { TotalRecordCount = 1 });
            var highProvider = new TestNextUpProvider(10, true, new QueryResult<BaseItem> { TotalRecordCount = 10 });

            var service = new NextUpService(new INextUpProvider[] { lowProvider, highProvider });

            var result = await service.GetNextUpAsync(query, options, CancellationToken.None).ConfigureAwait(false);

            Assert.False(lowProvider.WasCalled);
            Assert.True(highProvider.WasCalled);
            Assert.Equal(10, result.TotalRecordCount);
        }

        [Fact]
        public async Task GetNextUpAsync_SkipsProvidersThatCannotHandle()
        {
            var query = CreateQuery();
            var options = new DtoOptions();

            var skippedProvider = new TestNextUpProvider(10, false, new QueryResult<BaseItem>());
            var handlingProvider = new TestNextUpProvider(0, true, new QueryResult<BaseItem> { TotalRecordCount = 5 });

            var service = new NextUpService(new INextUpProvider[] { skippedProvider, handlingProvider });

            var result = await service.GetNextUpAsync(query, options, CancellationToken.None).ConfigureAwait(false);

            Assert.False(skippedProvider.WasCalled);
            Assert.True(handlingProvider.WasCalled);
            Assert.Equal(5, result.TotalRecordCount);
        }

        private static NextUpQuery CreateQuery()
        {
            return new NextUpQuery
            {
                User = new User("test", "auth", "reset")
            };
        }

        private sealed class TestNextUpProvider : INextUpProvider
        {
            private readonly bool _canHandle;
            private readonly QueryResult<BaseItem> _result;

            public TestNextUpProvider(int priority, bool canHandle, QueryResult<BaseItem> result)
            {
                Priority = priority;
                _canHandle = canHandle;
                _result = result;
            }

            public int Priority { get; }

            public bool WasCalled { get; private set; }

            public bool CanHandle(NextUpQuery query, IReadOnlyCollection<BaseItem>? parentFolders)
                => _canHandle;

            public Task<QueryResult<BaseItem>> GetNextUpAsync(
                NextUpQuery query,
                IReadOnlyCollection<BaseItem>? parentFolders,
                DtoOptions options,
                CancellationToken cancellationToken)
            {
                WasCalled = true;
                return Task.FromResult(_result);
            }
        }
    }
}
