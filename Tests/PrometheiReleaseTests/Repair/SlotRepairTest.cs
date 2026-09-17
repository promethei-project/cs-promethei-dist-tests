using PrometheiClient;
using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiContractsPlugin.Marketplace;
using PrometheiReleaseTests.Utils;
using FileUtils;
using Nethereum.Hex.HexConvertors.Extensions;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.Repair
{
    [NonParallelizable]
    [TestFixture]
    public class SlotRepairTest : MarketplaceAutoBootstrapDistTest
    {
        #region Setup

        private const int NumberOfFailures = 10;

        protected override int NumberOfHosts => 6;
        protected override int NumberOfClients => 1;
        protected override TestToken HostStartingBalance => PurchaseParams.Default.CollateralRequiredPerSlot * 1.1; // Each host can hold 1 slot.
        protected override TimeSpan HostAvailabilityMaxDuration => TimeSpan.FromDays(5.0);
        private TrackedFile originalFile = null!;

        #endregion

        private int proofsMissed = 0;

        [SetUp]
        public void Setup()
        {
            originalFile = GenerateTestFile(PurchaseParams.Default.UploadFilesize);
        }

        [Test]
        public void SingleFailure()
        {
            RollingRepairTest(
                numHostsPerFailure: 1
            );
        }

        [Test]
        public void DoubleFailure()
        {
            RollingRepairTest(
                numHostsPerFailure: 2
            );
        }

        private void RollingRepairTest(int numHostsPerFailure)
        {
            // Ensure all hosts will eventually be replaced:
            var totalReplacedHosts = NumberOfFailures * numHostsPerFailure;
            Assert.That(totalReplacedHosts, Is.GreaterThan(NumberOfHosts),
                "Test misconfigured: Not all original hosts will be replaced.");

            // Ensure enough hosts will survive each failure:
            var survivingHosts = NumberOfHosts - numHostsPerFailure;
            var minRequiredHosts = PurchaseParams.Default.Nodes - PurchaseParams.Default.Tolerance;
            Assert.That(survivingHosts, Is.GreaterThanOrEqualTo(minRequiredHosts),
                "Test misconfigured: Not enough hosts will survive failure to reconstruct dataset.");

            Log($"Using {NumberOfHosts} hosts.");
            Log($"{numHostsPerFailure} hosts will fail per failure step.");
            Log($"{survivingHosts} hosts will remain.");

            var (startHosts, clients, validator) = JumpStart();
            var hosts = startHosts.ToList();
            var client = clients.Single();

            proofsMissed = 0;
            var contract = CreateStorageRequest(client);
            contract.WaitForStorageContractStarted();
            // All slots are filled.

            client.Stop(waitTillStopped: true);

            HoldInitialSituation(hosts, contract);

            for (var i = 0; i < NumberOfFailures; i++)
            {
                PerformFailureStep(i, numHostsPerFailure, hosts, contract);
            }
        }

        private void HoldInitialSituation(List<IPrometheiNode> hosts, IStoragePurchaseContract contract)
        {
            Log("Holding initial situation to ensure contract is stable...");
            var config = GetContracts().Deployment.Config;
            WaitAndCheckNodesStaysAlive(config.PeriodDuration * 5, hosts);

            // No proofs were missed so far.
            Assert.That(proofsMissed, Is.EqualTo(0), $"Proofs were missed *BEFORE* any hosts were shut down.");

            var requestState = GetContracts().GetRequestState(contract.PurchaseId.HexToByteArray());
            Assert.That(requestState, Is.EqualTo(RequestState.Started));
        }

        private void PerformFailureStep(int i, int numHostsPerFailure, List<IPrometheiNode> hosts, IStoragePurchaseContract contract)
        {
            Log($"Failure step: {i}");
            Log($"Running hosts: [{string.Join(", ", hosts.Select(GetNameAndBalance))}]");
            Log("Blocks:");
            foreach (var h in hosts)
            {
                h.GetDatasetStatus(contract.EncodedContentId);
            }

            StartNewHosts(hosts, numHostsPerFailure);

            var selectedFills = SelectOldestSlotFills(hosts, numHostsPerFailure);
            var selectedSlots = selectedFills.Select(f => f.SlotFilledEvent.SlotIndex).ToArray();

            var eventStartUtc = DateTime.UtcNow;
            StopHostsOfSlotFills(hosts, selectedFills);

            WaitForSlotFreedEvents(eventStartUtc, contract, selectedSlots);
            WaitForNewSlotFilledEvents(eventStartUtc, contract, selectedSlots);
            AssertDataIsAvailable(originalFile, contract.EncodedContentId);
        }

        private SlotFill[] SelectOldestSlotFills(List<IPrometheiNode> hosts, int numHostsPerFailure)
        {
            var allFills = GetOnChainSlotFills(hosts);
            Log($"Current fills:{string.Join(string.Empty, allFills.Select(f => $"{Environment.NewLine}           - {f}"))}");
            return GetSlotFillsByOldestHost(numHostsPerFailure, allFills, hosts);
        }

        private void StopHostsOfSlotFills(List<IPrometheiNode> hosts, SlotFill[] selectedFills)
        {
            foreach (var fill in selectedFills)
            {
                Log($"Causing failure for host: {fill.Host.GetName()} slotIndex: {fill.SlotFilledEvent.SlotIndex}");
                hosts.Remove(fill.Host);
            }
            Parallel(selectedFills, f => f.Host.Stop(waitTillStopped: true));
        }

        private void StartNewHosts(List<IPrometheiNode> hosts, int numHostsPerFailure)
        {
            var newHosts = Parallel(numHostsPerFailure, StartOneHost);
            hosts.AddRange(newHosts);
        }

        private string GetNameAndBalance(IPrometheiNode host)
        {
            return $"{host.GetName()} = {GetTstBalance(host)}";
        }

        private void Parallel<T>(T[] source, Action<T> action)
        {
            var tasks = source.Select(s => Task.Run(() => action(s))).ToArray();
            Task.WaitAll(tasks);
        }

        private T[] Parallel<T>(int count, Func<T> task)
        {
            var tasks = new List<Task<T>>();
            for (var i = 0; i < count; i++)
            {
                tasks.Add(Task.Run(task));
            }
            Task.WaitAll(tasks);
            return tasks.Select(t => t.Result).ToArray();
        }

        protected override void OnPeriod(PeriodReport report)
        {
            report.Log(GetTestLog());

            proofsMissed += report.GetNumberOfProofsMissed();

            // There can't be any calls to FreeSlot.
            // We expect the validator to call MarkProofAsMissing
            // which should result in SlotFreed events.
            foreach (var c in report.FunctionCalls)
            {
                Assert.That(c.Name, Is.Not.EqualTo(nameof(FreeSlot1Function)));
                Assert.That(c.Name, Is.Not.EqualTo(nameof(FreeSlotFunction)));
            }
        }

        private void WaitForSlotFreedEvents(DateTime startUtc, IStoragePurchaseContract contract, ulong[] slotIndices)
        {
            var remaining = slotIndices.ToList();
            var timeout = CalculateContractFailTimespan();
            var context = GetLogContext(contract, slotIndices);
            Log($"{context} Timeout: {Time.FormatDuration(timeout)}");

            WaitForNewEventWithTimeout(
                startUtc: startUtc,
                timeoutUtc: DateTime.UtcNow + timeout,
                waiter: () => GetContracts().WaitUntilNextPeriod(),
                checker: events =>
                {
                    var slotsFreed = events.GetEvents<SlotFreedEventDTO>();

                    foreach (var free in slotsFreed)
                    {
                        var freedId = free.RequestId.ToHex().ToLowerInvariant();
                        Log($"{context} {free.Block} Free for requestId '{freedId}' slotIndex: {free.SlotIndex}");

                        if (freedId.Equals(contract.PurchaseId, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (remaining.Contains(free.SlotIndex))
                            {
                                Log($"{context} {free.Block} Found correct slotFree event. slotIndex: {free.SlotIndex}");
                                remaining.Remove(free.SlotIndex);
                                if (remaining.Count == 0)
                                {
                                    Log($"{context} Done! Found all required slotFree events.");
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                });
        }

        private void WaitForNewSlotFilledEvents(DateTime startUtc, IStoragePurchaseContract contract, ulong[] slotIndices)
        {
            var remaining = slotIndices.ToList();
            var timeout = contract.Purchase.PurchaseParams.Expiry;
            var context = GetLogContext(contract, slotIndices);
            Log($"{context} Timeout: {Time.FormatDuration(timeout)}");

            WaitForNewEventWithTimeout(
                startUtc: startUtc,
                timeoutUtc: DateTime.UtcNow + timeout,
                waiter: () => Thread.Sleep(TimeSpan.FromSeconds(15)),
                checker: events =>
                {
                    var slotFillEvents = events.GetEvents<SlotFilledEventDTO>();
                    var matches = slotFillEvents.Where(f =>
                    {
                        return
                            f.RequestId.ToHex().ToLowerInvariant() == contract.PurchaseId.ToLowerInvariant() &&
                            remaining.Contains(f.SlotIndex);
                    }).ToArray();

                    foreach (var match in matches)
                    {
                        Log($"{context} Found correct new slotFilled event. slotIndex: {match.SlotIndex}");
                        remaining.Remove(match.SlotIndex);
                    }
                    if (remaining.Count == 0)
                    {
                        Log($"{context} Done! Found all required slotFilled events.");
                        return true;
                    }
                    return false;
                });
        }

        private void WaitForNewEventWithTimeout(DateTime startUtc, DateTime timeoutUtc, Action waiter, Func<IPrometheiContractsEvents, bool> checker)
        {
            var loopEnd = startUtc;
            Thread.Sleep(TimeSpan.FromSeconds(3.0));

            while (DateTime.UtcNow < timeoutUtc)
            {
                var loopStart = loopEnd;
                loopEnd = DateTime.UtcNow;
                var events = GetContracts().GetEvents(new TimeRange(loopStart, loopEnd));
                if (checker(events)) return;
                waiter();
            }
            Assert.Fail($"{nameof(WaitForNewEventWithTimeout)} Failed. TimeoutUTC: {Time.FormatTimestamp(timeoutUtc)}");
        }

        private string GetLogContext(IStoragePurchaseContract contract, ulong[] slotIndices)
        {
            return $"(slotIndices: {string.Join(",", slotIndices.Select(i => i.ToString()))}) - ";
        }

        private SlotFill[] GetSlotFillsByOldestHost(int number, SlotFill[] fills, List<IPrometheiNode> hosts)
        {
            var copy = hosts.ToArray();
            var result = new List<SlotFill>();
            foreach (var host in copy)
            {
                var fill = GetFillByHost(host, fills);
                if (fill == null)
                {
                    // This host didn't fill anything.
                    // Move this one to the back of the list.
                    hosts.Remove(host);
                    hosts.Add(host);
                }
                else
                {
                    result.Add(fill);
                    if (result.Count == number) return result.ToArray();
                }
            }
            throw new Exception($"Could not find {number} hosts that have filled a slot.");
        }

        private SlotFill? GetFillByHost(IPrometheiNode host, SlotFill[] fills)
        {
            // If these is more than 1 fill by this host, the test is misconfigured.
            // The collateral balance of the host should guarantee it can fill 1 slot maximum.
            return fills.SingleOrDefault(f => f.Host.EthAddress == host.EthAddress);
        }

        private IStoragePurchaseContract CreateStorageRequest(IPrometheiNode client)
        {
            var cid = client.UploadFile(originalFile);
            return client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid, p => p
                .WithDuration(HostAvailabilityMaxDuration / 2)
                .WithProofProbability(1)
            ));
        }
    }
}
