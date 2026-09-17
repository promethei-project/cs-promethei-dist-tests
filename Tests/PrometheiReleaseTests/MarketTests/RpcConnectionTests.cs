using PrometheiClient;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiReleaseTests.Utils;
using GethPlugin;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class RpcConnectionTests : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => 4;
        protected override int NumberOfClients => 1;

        private IGethNode rpcNode = null!;
        private int numMarkedAsMissing = 0;

        [SetUp]
        public void Setup()
        {
            // Unusual setup: We're using an extra geth node that follows the miner.
            // We can stop and restart it, disconnecting promethei nodes
            // without screwing with the testing infra that's monitoring the chain.
            rpcNode = StartGethNode(s => s.WithName("follower").WithBootstrapNode(GetGeth()));
        }

        [Test]
        [Combinatorial]
        public void HostRecoversSlotAfterDisconnect(
            [Values(0, 15)] int delayMinutes)
        {
            var hosts = StartHosts(s => s
                    .EnableMarketplace(rpcNode, GetContracts(), s => s
                    .WithInitial(StartingBalanceEth.Eth(), HostStartingBalance)
                    .AsStorageNode()));
            var client = StartClients().Single();

            var request = CreateStorageRequest(client);
            request.WaitForStorageContractStarted();

            var fills = GetOnChainSlotFills(hosts);
            // We select one host that has filled at least one slot.
            var host = fills.First().Host;
            var slots = host.Marketplace.GetSlots();
            // We select one slot of this host.
            var slot = slots.First();

            Log("The state of a successfully started slot should be 'proving'.");
            Assert.That(slot.State, Is.EqualTo(StorageSlotState.Proving));

            Log("The RPC connection provider goes down.");
            rpcNode.Pause();

            Log("We expect the host to report and error state for this slot.");
            WaitUntilSlotState(host, slot.SlotId, StorageSlotState.Errored);

            Sleep(TimeSpan.FromMinutes(delayMinutes));

            Log("We restore the RPC connecting...");
            rpcNode.Resume();

            Log("The host's slot should recover.");
            WaitUntilSlotState(host, slot.SlotId, StorageSlotState.Proving);
        }

        [Test]
        [Combinatorial]
        public void ValidatorRecoversAfterDisconnect(
            [Values(0, 15)] int delayMinutes)
        {
            var (hosts, clients) = JumpStartHostsAndClients();
            var client = clients.Single();
            var validator = StartValidator(s => s
                    .EnableMarketplace(rpcNode, GetContracts(), s => s
                    .WithInitial(StartingBalanceEth.Eth(), HostStartingBalance)
                    .AsValidator()));

            var request = CreateStorageRequest(client);
            request.WaitForStorageContractStarted();

            // Select a host to stop.
            var fills = GetOnChainSlotFills(hosts);
            var select = fills.First();
            var host = select.Host;
            Log($"Host {host.GetName()} is holding slot {select.SlotFilledEvent.SlotIndex} and is selected to be stopped.");

            Log("The RPC connection provider goes down.");
            rpcNode.Pause();

            Log("Oh no the host goes offline, but there's no validator to detect it.");
            host.Stop(waitTillStopped: true);

            // There's no way to check the validator status directly. We wait a while.
            Log("We wait 5 periods. That should be enough to free the slot if the validator was looking.");
            Sleep(GetPeriodDuration() * 5);

            Assert.That(numMarkedAsMissing, Is.EqualTo(0));
            Log("But the validator was offline, so no proof was marked as missing.");

            Sleep(TimeSpan.FromMinutes(delayMinutes));

            Log("The RPC connection provider is restored.");
            rpcNode.Resume();

            var allowedTimeout = CalculateAllowedTimeout(delayMinutes);
            Log("The validator should resume work and mark proofs as missing.");
            Log("Because the validator uses an exponential-backoff for the RPC connection,");
            Log($"a delay of {delayMinutes} minutes implies an allowed timeout of {Time.FormatDuration(allowedTimeout)}");
            Time.WaitUntil(() => numMarkedAsMissing > 0,
                timeout: allowedTimeout,
                retryDelay: TimeSpan.FromSeconds(10),
                msg: "At least 1 proof is marked as missing."
            );
        }

        protected override void OnPeriod(PeriodReport report)
        {
            foreach (var r in report.Requests)
            {
                foreach (var s in r.Slots)
                {
                    if (s.MarkedAsMissing)
                    {
                        Log($"Proof for slot {s.Index} is marked as missing in period {report.Period.PeriodNumber}");
                        numMarkedAsMissing++;
                    }
                }
            }
        }

        private void WaitUntilSlotState(IPrometheiNode host, string slotId, StorageSlotState expected)
        {
            Log($"{expected}");
            Time.WaitUntil(() =>
                {
                    try
                    {
                        var actual = host.Marketplace.GetSlot(slotId).State;
                        if (actual == expected)
                        {
                            Log($"Successfully detected slot state {expected}");
                            return true;
                        }
                        return false;
                    }
                    catch (TimeoutException timeout)
                    {
                        if (timeout.InnerException is AggregateException agg1)
                        {
                            if (agg1.InnerException is AggregateException agg2)
                            {
                                if (agg2.InnerException is PrometheiOpenApi.ApiException apiException)
                                {
                                    if (expected == StorageSlotState.Errored &&
                                        apiException.Message.Contains("Host is not in an active sale for the slot"))
                                    {
                                        Log("Successfully detected errored slot state.");
                                        return true;
                                    }
                                }
                            }
                        }
                        return false;
                    }
                },
                timeout: GetPeriodDuration() * 2,
                retryDelay: TimeSpan.FromSeconds(10),
                msg: $"{nameof(WaitUntilSlotState)} == {expected}"
            );
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
        {
            var cid = client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize));
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithDuration(HostAvailabilityMaxDuration * 0.75)
                .WithProofProbability(1)
            ));
        }

        private TimeSpan CalculateAllowedTimeout(int delayMinutes)
        {
            var gracePeriod = 10 * GetPeriodDuration();
            var delaySeconds = 1;
            while (delaySeconds < (delayMinutes * 60))
            {
                delaySeconds += delaySeconds + 1;
            }
            delaySeconds += delaySeconds + 1;
            return TimeSpan.FromSeconds(delaySeconds) + gracePeriod;
        }
    }
}
