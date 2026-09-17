using PrometheiClient;
using PrometheiPlugin;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class AvailabilitySaturationTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => PurchaseParams.Default.Nodes;
        protected override int NumberOfClients => 5;
        protected override bool MonitorProofPeriods => false;
        protected override TestToken HostStartingBalance => PurchaseParams.Default.CollateralRequiredPerSlot * 1.1; // Each host can hold 1 slot.

        [Test]
        public void AvailabilityTest()
        {
            var (hosts, clients, validator) = JumpStart();
            
            // We want to create many concurrent purchase requests
            // to flood the host worker queues.
            // The requests are more than enough to fill
            // the host quota. So, we don't mind
            // if some of them become cancelled.
            // The idea is that concurrency issues in the node
            // causes the accounting of used space
            // to drift from the real used space, eventually
            // saturating the node with unused but locked up space.

            Log("All hosts are empty at the start...");
            AssertHostsAreEmpty(hosts);

            for (int i = 0; i < 5; i++)
            {
                Cycle(i, hosts, clients);
            }

            Log("Now we create one purchase, and expect it to start.");
            var client = clients.First();
            var purchase = client.Marketplace
                .RequestStorage(new StoragePurchaseRequest(
                client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize))
            ));
            purchase.WaitForStorageContractStarted();
        }

        private void Cycle(int i, IPrometheiNodeGroup hosts, IPrometheiNodeGroup clients)
        {
            Log("Cycle " + i);
            Log("Uploading files...");
            var pairs = clients.Select(c =>
            {
                var cid = c.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
                return (c, cid);
            }
            ).ToArray();

            // Start all purchases as close to simultaneous as possible:
            Log("Creating purchases...");
            var tasks = pairs.Select(pair => Task.Run(() =>
            {
                return pair.c.Marketplace.RequestStorage(new StoragePurchaseRequest(pair.cid));
            })).ToArray();

            Task.WaitAll(tasks);
            var requests = tasks.Select(t => t.Result).ToArray();

            Log("Waiting for all purchases to finish or cancel...");
            Time.WaitUntil(() =>
            {
                return requests.All(r =>
                {
                    var state = r.GetStatus();
                    return state != null &&
                        (state.IsCancelled || state.IsFinished);
                });
            },
            timeout: TimeSpan.FromMinutes(30.0),
            retryDelay: TimeSpan.FromMinutes(1.0),
            msg: "All purchases finished or cancelled.");

            Log("All purchases are finished or cancelled. All hosts should be empty...");
            AssertHostsAreEmpty(hosts);
        }
    }
}
