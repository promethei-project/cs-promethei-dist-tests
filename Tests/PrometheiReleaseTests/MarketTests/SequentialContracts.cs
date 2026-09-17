using PrometheiClient;
using PrometheiPlugin;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [NonParallelizable]
    [TestFixture(6, 4, 2, 20)]
    [TestFixture(5, 10, 5, 10)]
    public class SequentialContracts : MarketplaceAutoBootstrapDistTest
    {
        public SequentialContracts(int hosts, int slots, int tolerance, int sizeMb)
        {
            this.hosts = hosts;
            purchaseParams = PurchaseParams.Default
                .WithUploadFilesize(sizeMb.MB())
                .WithNodes(slots)
                .WithTolerance(tolerance)
                .WithProofProbability(100000);
        }

        private readonly int hosts;
        private readonly PurchaseParams purchaseParams;

        protected override int NumberOfHosts => hosts;
        protected override int NumberOfClients => 4;

        [Test]
        [Combinatorial]
        public void Sequential(
            [Values(5)] int numGenerations)
        {
            var (_, clients, _) = JumpStart();

            for (var i = 0; i < numGenerations; i++)
            {
                Log("Generation: " + i);
                try
                {
                    Generation(clients);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed at generation {i} with exception {ex}");
                }
            }

            Sleep(TimeSpan.FromSeconds(12.0));
        }

        private void Generation(IPrometheiNodeGroup clients)
        {
            var requests = All(clients.ToArray(), CreateStorageRequest);

            All(requests, r =>
            {
                r.WaitForStorageContractSubmitted();
                AssertContractIsOnChain(r);
            });

            All(requests, WaitForContractStarted);
        }

        private void All<T>(T[] items, Action<T> action)
        {
            var tasks = items.Select(r => Task.Run(() => action(r))).ToArray();
            Task.WaitAll(tasks);
            foreach(var t in tasks)
            {
                if (t.Exception != null) throw t.Exception;
            }
        }

        private TResult[] All<T, TResult>(T[] items, Func<T, TResult> action)
        {
            var tasks = items.Select(r => Task.Run(() => action(r))).ToArray();
            Task.WaitAll(tasks);
            foreach (var t in tasks)
            {
                if (t.Exception != null) throw t.Exception;
            }
            return tasks.Select(t => t.Result).ToArray();
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
        {
            var cid = client.UploadFile(GenerateTestFile(purchaseParams.UploadFilesize));
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, purchaseParams));
        }
    }
}
