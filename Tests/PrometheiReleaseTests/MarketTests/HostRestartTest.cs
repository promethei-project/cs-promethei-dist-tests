using PrometheiClient;
using PrometheiContractsPlugin.Marketplace;
using PrometheiReleaseTests.Utils;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture]
    public class HostRestartTest : MarketplaceAutoBootstrapDistTest
    {
        protected override int NumberOfHosts => PurchaseParams.Default.Nodes * 2;
        protected override TestToken HostStartingBalance => PurchaseParams.Default.CollateralRequiredPerSlot * 1.1; // Each host can hold 1 slot.
        protected override int NumberOfClients => 1;

        [Test]
        public void HostsRestart()
        {
            var (hosts, clients, validator) = JumpStart();
            var client = clients.Single();

            var purchase = client.Marketplace.RequestStorage(new StoragePurchaseRequest(
                client.UploadFile(GenerateTestFile(PurchaseParams.Default.UploadFilesize)),
                p => p
                    .WithDuration(HostAvailabilityMaxDuration - TimeSpan.FromSeconds(10))
                    .WithExpiry(TimeSpan.FromHours(1))
                    .WithProofProbability(1)
            ));

            purchase.WaitForStorageContractStarted();

            var fills = GetOnChainSlotFills(hosts);

            foreach (var f in fills)
            {
                // Each host that has filled a slot, becomes restarted one by one.
                // Slots may become freed, but other hosts should pick those up.
                f.Host.InPlaceRestart();
            }

            var expectedSlotFreedEvents = new List<ulong>();
            // For each restarted host, it should report its filled slot.
            // If it doesn't, a SlotFreed event for that slot must be present.
            foreach (var f in fills)
            {
                var slots = f.Host.Marketplace.GetSlots();

                if (slots == null ||
                    slots.Length == 0 ||
                    !slots.Any(s => s.SlotIndex == Convert.ToInt64(f.SlotFilledEvent.SlotIndex)))
                {
                    // Expect a slot-freed event for this slot.
                    expectedSlotFreedEvents.Add(f.SlotFilledEvent.SlotIndex);
                }
                else
                {
                    Log($"Host {f.Host.GetName()} remembers its slot {f.SlotFilledEvent.SlotIndex}");
                }
            }

            WaitUntilProofsSubmitted(purchase.Purchase.PurchaseParams.Nodes);

            if (expectedSlotFreedEvents.Count == 0)
            {
                Log("No slotFreed events expected.");
                return;
            }

            var freeEvents = GetContracts().GetEvents(GetTestRunTimeRange()).GetEvents<SlotFreedEventDTO>();
            foreach (var expected in expectedSlotFreedEvents)
            {
                var match = freeEvents.SingleOrDefault(f => f.SlotIndex == expected);
                if (match == null)
                {
                    Assert.Fail($"Expected a slotFreed event for slot {expected}, but found " +
                        $"'{string.Join(", ", freeEvents.Select(f => f.SlotIndex))}'");
                }
            }
        }

        private void WaitUntilProofsSubmitted(int slots)
        {
            Log("Waiting until a proof is submitted by each host...");
            var start = DateTime.UtcNow;
            var contracts = GetContracts();
            var requests = GetChainMonitor().Requests.ToArray();
            var seenIndices = new HashSet<int>();

            Time.WaitUntil(() =>
            {
                var end = DateTime.UtcNow;
                var interval = new TimeRange(start, end);
                start = end;

                var events = GetContracts().GetEvents(interval).GetEvents<ProofSubmittedEventDTO>();
                foreach (var e in events)
                {
                    var proofOrigin = e.FindProofOrigin(contracts, requests);
                    if (proofOrigin != null)
                    {
                        var slotIndex = proofOrigin.SlotIndex;
                        seenIndices.Add(slotIndex);
                        Log("Seen proof for " + slotIndex);
                    }
                }

                return seenIndices.Count == slots;
            },
            timeout: contracts.Deployment.Config.PeriodDuration * 10,
            retryDelay: TimeSpan.FromSeconds(30),
            msg: nameof(WaitUntilProofsSubmitted));
        }
    }
}
