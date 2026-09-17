using PrometheiClient;
using PrometheiContractsPlugin;
using PrometheiContractsPlugin.ChainMonitor;
using PrometheiContractsPlugin.Marketplace;
using PrometheiPlugin;
using PrometheiTests;
using FileUtils;
using GethPlugin;
using Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.Utils
{
    public abstract class MarketplaceAutoBootstrapDistTest : AutoBootstrapDistTest, IPeriodMonitorEventHandler
    {
        private MarketplaceHandle handle = null!;
        protected const int StartingBalanceTST = 1000;
        protected const int StartingBalanceEth = 10;

        [SetUp]
        public void SetupMarketplace()
        {
            var geth = StartGethNode(s => s.WithName("geth").IsMiner());
            var contracts = Ci.StartPrometheiContracts(s => {
                s.WithRpcNode(geth);
                s.WithVersionInfo(BootstrapNode.Version);
                OnDeployContracts(s);
            });
            // Do not use TestRunTimeRange().From to initialize the chain monitor:
            // It'll find its earliest timestamps in the pre-mined blocks in the geth image
            // and completely screw with chain state tracking.
            // Use this moment, the start of the contract deployment instead.
            var monitor = SetupChainMonitor(GetTestLog(), geth, contracts, DateTime.UtcNow);
            handle = new MarketplaceHandle(geth, contracts, monitor);
        }

        [TearDown]
        public void TearDownMarketplace()
        {
            if (handle != null && handle.ChainMonitor != null) handle.ChainMonitor.Stop();
        }

        protected IGethNode GetGeth()
        {
            return handle.Geth;
        }

        protected IPrometheiContracts GetContracts()
        {
            return handle.Contracts;
        }

        protected ChainMonitor GetChainMonitor()
        {
            if (handle.ChainMonitor == null) throw new Exception($"Make sure {nameof(MonitorChainState)} is set to true.");
            return handle.ChainMonitor;
        }

        protected TimeSpan GetPeriodDuration()
        {
            var config = GetContracts().Deployment.Config;
            return TimeSpan.FromSeconds(config.Proofs.Period);
        }

        protected abstract int NumberOfHosts { get; }
        protected abstract int NumberOfClients { get; }
        protected virtual TestToken HostStartingBalance => StartingBalanceTST.Tst();
        protected virtual ByteSize HostQuota => 2.GB();
        protected virtual TimeSpan HostAvailabilityMaxDuration => TimeSpan.FromHours(3.0);
        protected virtual bool MonitorChainState { get; } = true;
        protected virtual bool MonitorProofPeriods { get; } = true;
        protected virtual bool LogPeriodReports { get; } = false;
        
        protected TestToken DefaultAvailabilityMaxCollateralPerByte => 999999.Tst();

        protected virtual TimeSpan HostBlockTTL
        {
            get
            {
                // Blocks are downloaded using the default TTL when slots are being filled.
                // If the block expiries are not updated to match the storage contract
                // within 15 period durations, we assume it failed and the data should
                // be cleaned up.
                return GetPeriodDuration() * 15;
            }
        }

        protected virtual int HostBlockMaintenanceCount => 1000;

        protected virtual TimeSpan HostBlockMaintenanceInterval
        {
            get
            {
                return HostBlockTTL / 10;
            }
        }

        protected virtual void OnPeriod(PeriodReport report)
        {
        }

        protected virtual void OnDeployContracts(IPrometheiContractsSetup setup)
        {
            Log("TODO: In the current version, nodes will reserve slots even when they don't have enough tokens to fill them.");
            Log("TODO: This causes the test to fail sometimes: A slot becomes reserved by 3 (default max reservations) hosts");
            Log("TODO: that are already hosting a slot. They will fail to fill it, and the reservations will prevent anyone");
            Log("TODO: else from filling the slot.");
            Log("TODO: Github issue: https://github.com/promethei-project/nim-promethei-node/issues/87");
            Log("TODO: For now, we override the max-slot-reservations configuration.");
            setup.WithMaxReservationsOverride(20);
        }

        public (IPrometheiNodeGroup, IPrometheiNodeGroup) JumpStartHostsAndClients()
        {
            IPrometheiNodeGroup hosts = null!;
            IPrometheiNodeGroup clients = null!;
            var tasks = new Task[]
            {
                Task.Run(() => hosts = StartHosts()),
                Task.Run(() => clients = StartClients())
            };
            Task.WaitAll(tasks);
            return (hosts, clients);
        }

        public (IPrometheiNodeGroup, IPrometheiNodeGroup, IPrometheiNode) JumpStart()
        {
            IPrometheiNodeGroup hosts = null!;
            IPrometheiNodeGroup clients = null!;
            IPrometheiNode validator = null!;
            var tasks = new Task[]
            {
                Task.Run(() => hosts = StartHosts()),
                Task.Run(() => clients = StartClients()),
                Task.Run(() => validator = StartValidator())
            };
            Task.WaitAll(tasks);
            return (hosts, clients, validator);
        }

        public IPrometheiNodeGroup StartHosts()
        {
            return StartHosts(s => { });
        }

        public IPrometheiNodeGroup StartHosts(Action<IPrometheiSetup> additional)
        {
            var hosts = StartPromethei(NumberOfHosts, s =>
            {
                s
                .WithName("host")
                .WithStorageQuota(HostQuota)
                .WithBlockTTL(HostBlockTTL)
                .WithBlockMaintenanceNumber(HostBlockMaintenanceCount)
                .WithBlockMaintenanceInterval(HostBlockMaintenanceInterval)
                .EnableMarketplace(GetGeth(), GetContracts(), m => m
                    .WithInitial(StartingBalanceEth.Eth(), HostStartingBalance)
                    .AsStorageNode()
                );
                additional(s);
            });

            var config = GetContracts().Deployment.Config;
            foreach (var host in hosts)
            {
                AssertTstBalance(host, HostStartingBalance, nameof(StartHosts));
                AssertEthBalance(host, StartingBalanceEth.Eth(), nameof(StartHosts));
                
                host.Marketplace.MakeStorageAvailable(new CreateStorageAvailability(
                    untilUtc: DateTime.UtcNow + TimeSpan.FromDays(30.0),
                    maxDuration: HostAvailabilityMaxDuration,
                    minPricePerBytePerSecond: 1.TstWei(),
                    maxCollateralPerByte: DefaultAvailabilityMaxCollateralPerByte)
                );
            }
            return hosts;
        }

        public IPrometheiNode StartOneHost()
        {
            var host = StartPromethei(s => s
                .WithName("singlehost")
                .WithStorageQuota(HostQuota)
                .WithBlockTTL(HostBlockTTL)
                .WithBlockMaintenanceNumber(HostBlockMaintenanceCount)
                .WithBlockMaintenanceInterval(HostBlockMaintenanceInterval)
                .EnableMarketplace(GetGeth(), GetContracts(), m => m
                    .WithInitial(StartingBalanceEth.Eth(), HostStartingBalance)
                    .AsStorageNode()
                )
            );

            var config = GetContracts().Deployment.Config;
            AssertTstBalance(host, HostStartingBalance, nameof(StartOneHost));
            AssertEthBalance(host, StartingBalanceEth.Eth(), nameof(StartOneHost));

            host.Marketplace.MakeStorageAvailable(new CreateStorageAvailability(
                untilUtc: DateTime.UtcNow + TimeSpan.FromDays(30.0),
                maxDuration: HostAvailabilityMaxDuration,
                minPricePerBytePerSecond: 1.TstWei(),
                maxCollateralPerByte: 999999.Tst())
            );
            return host;
        }

        public void AssertHostsAreEmpty(IEnumerable<IPrometheiNode> hosts)
        {
            AssertHostHasNoActiveSlots(hosts);
            AssertQuotaIsEmpty(hosts);
        }

        public void AssertHostHasNoActiveSlots(IEnumerable<IPrometheiNode> hosts)
        {
            Log($"{nameof(AssertHostHasNoActiveSlots)}...");
            var retry = GetBlockTTLAssertRetry();
            retry.Run(() =>
            {
                foreach (var n in hosts)
                {
                    var slots = n.Marketplace.GetSlots();
                    if (slots.Length > 0)
                    {
                        throw new Exception($"Host {n.GetName()} has {slots.Length} slots. Expected 0.");
                    }
                }
            });
            Log($"{nameof(AssertHostHasNoActiveSlots)} OK");
        }

        public void AssertQuotaIsEmpty(IEnumerable<IPrometheiNode> nodes)
        {
            Log($"{nameof(AssertQuotaIsEmpty)}...");
            var retry = GetBlockTTLAssertRetry();
            retry.Run(() =>
            {
                foreach (var n in nodes)
                {
                    var space = n.Space();
                    if (space.QuotaUsedBytes > 0)
                    {
                        throw new Exception($"Host {n.GetName()} has {space.QuotaUsedBytes} quota-bytes-used. Expected 0.");
                    }
                }
            });
            Log($"{nameof(AssertQuotaIsEmpty)} OK");
        }

        public void AssertTstBalance(IPrometheiNode node, TestToken expectedBalance, string message)
        {
            AssertTstBalance(node.EthAddress, expectedBalance, message);
        }

        public void AssertTstBalance(EthAddress address, TestToken expectedBalance, string message)
        {
            var retry = GetBalanceAssertRetry();
            retry.Run(() =>
            {
                var balance = GetTstBalance(address);

                if (balance != expectedBalance)
                {
                    throw new Exception(nameof(AssertTstBalance) +
                        $" expected: {expectedBalance} but was: {balance} - message: " + message);
                }
            });
        }

        public void AssertEthBalance(IPrometheiNode node, Ether expectedBalance, string message)
        {
            var retry = GetBalanceAssertRetry();
            retry.Run(() =>
            {
                var balance = GetEthBalance(node);

                if (balance != expectedBalance)
                {
                    throw new Exception(nameof(AssertEthBalance) + 
                        $" expected: {expectedBalance} but was: {balance} - message: " + message);
                }
            });
        }

        protected void AssertNoSlotsFreed(PeriodReport report)
        {
            var calls = report.GetSlotsFreedCalls();
            Assert.That(calls.Length, Is.EqualTo(0), $"FreeSlot calls: {string.Join(",", calls.Select(c => c.ToString()))}");
        }

        protected void AssertDataIsAvailable(TrackedFile originalFile, ContentId contentId)
        {
            Log("...");
            var checker = StartPromethei(s => s.WithName("checker"));
            var received = checker.DownloadContent(contentId);
            originalFile.AssertIsEqual(received);

            Log("Data is available.");
            checker.Stop(waitTillStopped: false);
        }

        public IPrometheiNodeGroup StartClients()
        {
            return StartClients(s => { });
        }

        public IPrometheiNodeGroup StartClients(Action<IPrometheiSetup> additional)
        {
            return StartPromethei(NumberOfClients, s =>
            {
                s.WithName("client")
                    .EnableMarketplace(GetGeth(), GetContracts(), m => m
                    .WithInitial(StartingBalanceEth.Eth(), StartingBalanceTST.Tst()));

                additional(s);
            });
        }

        public IPrometheiNode StartValidator()
        {
            return StartValidator(s => { });
        }

        public IPrometheiNode StartValidator(Action<IPrometheiSetup> additional)
        {
            return StartPromethei(s =>
            {
                s.WithName("validator")
                    .EnableMarketplace(GetGeth(), GetContracts(), m => m
                        .WithInitial(StartingBalanceEth.Eth(), StartingBalanceTST.Tst())
                        .AsValidator()
                    );
                additional(s);
            });
        }

        public bool GetLogPeriodReports()
        {
            return LogPeriodReports;
        }

        public void OnPeriodReport(PeriodReport report)
        {
            OnPeriod(report);
        }

        public SlotFill[] GetOnChainSlotFills(IEnumerable<IPrometheiNode> possibleHosts, string purchaseId)
        {
            var fills = GetOnChainSlotFills(possibleHosts);
            return fills.Where(f => f
                .SlotFilledEvent.RequestId.ToHex(false).ToLowerInvariant() == purchaseId.ToLowerInvariant())
                .ToArray();
        }

        public SlotFill[] KeepOnlyRecent(SlotFill[] fills)
        {
            // It's possible multiple fills exist for the same slot.
            // (there was a free and then a repair)
            // keep only the most recent fills per slot.

            var map = new Dictionary<ulong, SlotFill>();
            foreach (var f in fills)
            {
                var slotIndex = f.SlotFilledEvent.SlotIndex;
                if (!map.ContainsKey(slotIndex)) map.Add(slotIndex, f);
                else
                {
                    if (map[slotIndex].SlotFilledEvent.Block.Utc < f.SlotFilledEvent.Block.Utc)
                    {
                        map[slotIndex] = f;
                    }
                }
            }
            return map.Values.ToArray();
        }

        public SlotFill[] GetOnChainSlotFills(IEnumerable<IPrometheiNode> possibleHosts)
        {
            var events = GetContracts().GetEvents(GetTestRunTimeRange());
            var fills = events.GetEvents<SlotFilledEventDTO>();
            return fills.Select(f =>
            {
                // We can encounter a fill event that's from an old host.
                // We must disregard those.
                var host = possibleHosts.SingleOrDefault(h => h.EthAddress.Address == f.Host.Address);
                if (host == null) return null;
                return new SlotFill(f, host);
            })
            .Where(f => f != null)
            .Cast<SlotFill>()
            .ToArray();
        }

        protected void AssertClientHasPaidForContract(TestToken pricePerBytePerSecond, IPrometheiNode client, IStoragePurchaseContract contract, IPrometheiNodeGroup hosts)
        {
            var expectedBalance = StartingBalanceTST.Tst() - GetContractFinalCost(pricePerBytePerSecond, contract, hosts);

            AssertTstBalance(client, expectedBalance, "Client balance incorrect.");

            Log($"Client has paid for contract. Balance: {expectedBalance}");
        }

        protected void AssertHostsWerePaidForContract(TestToken pricePerBytePerSecond, IStoragePurchaseContract contract, IPrometheiNodeGroup hosts)
        {
            var fills = GetOnChainSlotFills(hosts);
            var submitUtc = GetContractOnChainSubmittedUtc(contract);
            var finishUtc = submitUtc + contract.Purchase.PurchaseParams.Duration;
            var slotSize = Convert.ToInt64(contract.GetStatus()!.Request.Ask.SlotSize).Bytes();
            var expectedBalances = new Dictionary<EthAddress, TestToken>();

            foreach (var host in hosts) expectedBalances.Add(host.EthAddress, HostStartingBalance);
            foreach (var fill in fills)
            {
                var slotDuration = finishUtc - fill.SlotFilledEvent.Block.Utc;
                expectedBalances[fill.Host.EthAddress] += GetContractCostPerSlot(pricePerBytePerSecond, slotSize, slotDuration);
            }

            foreach (var pair in expectedBalances)
            {
                AssertTstBalance(pair.Key, pair.Value, $"Host {pair.Key} was not paid for storage.");

                Log($"Host {pair.Key} was paid for storage. Balance: {pair.Value}");
            }
        }

        protected void AssertHostsCollateralsAreUnchanged(IPrometheiNodeGroup hosts)
        {
            // There is no separate collateral location yet.
            // All host balances should be equal to or greater than the starting balance.
            foreach (var host in hosts)
            {
                var retry = GetBalanceAssertRetry();
                retry.Run(() =>
                {
                    if (GetTstBalance(host) < HostStartingBalance)
                    {
                        throw new Exception(nameof(AssertHostsCollateralsAreUnchanged));
                    }
                });
            }
        }

        protected void WaitForContractStarted(IStoragePurchaseContract r)
        {
            try
            {
                r.WaitForStorageContractStarted();
            }
            catch
            {
                // Contract failed to start. Retrieve and log every call to ReserveSlot to identify which hosts
                // should have filled the slot.

                var requestId = r.PurchaseId.ToLowerInvariant();
                var calls = new List<ReserveSlotFunction>();
                GetContracts().GetEvents(GetTestRunTimeRange()).GetReserveSlotCalls(c =>
                {
                    if (c.RequestId.ToHex().ToLowerInvariant() == requestId) calls.Add(c);
                });

                Log($"Request '{requestId}' failed to start. There were {calls.Count} hosts who called reserve-slot for it:");
                foreach (var c in calls)
                {
                    Log($" - {c.Block.Utc} Host: {c.FromAddress} RequestId: {c.RequestId.ToHex()} SlotIndex: {c.SlotIndex}");
                }
                throw;
            }
        }

        private ChainMonitor? SetupChainMonitor(ILog log, IGethNode gethNode, IPrometheiContracts contracts, DateTime startUtc)
        {
            if (!MonitorChainState) return null;

            var result = new ChainMonitor(log, gethNode, contracts, this, startUtc, 
                updateInterval: TimeSpan.FromSeconds(3.0),
                monitorProofPeriods: MonitorProofPeriods);

            result.Start(() =>
            {
                Assert.Fail("Failure in chain monitor.");
            });
            return result;
        }

        private Retry GetBalanceAssertRetry()
        {
            return new Retry("AssertBalance",
                maxTimeout: TimeSpan.FromMinutes(10.0),
                sleepAfterFail: TimeSpan.FromSeconds(10.0),
                onFail: f => { },
                failFast: false);
        }

        private Retry GetBlockTTLAssertRetry()
        {
            return new Retry("AssertWithBlockTTLTimeout",
                maxTimeout: HostBlockTTL * 3,
                sleepAfterFail: TimeSpan.FromSeconds(30.0),
                onFail: f => { },
                failFast: false);
        }

        protected TestToken GetTstBalance(IPrometheiNode node)
        {
            return GetContracts().GetTestTokenBalance(node);
        }

        protected TestToken GetTstBalance(EthAddress address)
        {
            return GetContracts().GetTestTokenBalance(address);
        }

        private Ether GetEthBalance(IPrometheiNode node)
        {
            return GetGeth().GetEthBalance(node);
        }

        private Ether GetEthBalance(EthAddress address)
        {
            return GetGeth().GetEthBalance(address);
        }

        private TestToken GetContractFinalCost(TestToken pricePerBytePerSecond, IStoragePurchaseContract contract, IPrometheiNodeGroup hosts)
        {
            var fills = GetOnChainSlotFills(hosts);
            var result = 0.Tst();
            var submitUtc = GetContractOnChainSubmittedUtc(contract);
            var finishUtc = submitUtc + contract.Purchase.PurchaseParams.Duration;
            var slotSize = Convert.ToInt64(contract.GetStatus()!.Request.Ask.SlotSize).Bytes();

            foreach (var fill in fills)
            {
                var slotDuration = finishUtc - fill.SlotFilledEvent.Block.Utc;
                result += GetContractCostPerSlot(pricePerBytePerSecond, slotSize, slotDuration);
            }

            return result;
        }

        private DateTime GetContractOnChainSubmittedUtc(IStoragePurchaseContract contract)
        {
            return Time.Retry(() =>
            {
                var events = GetContracts().GetEvents(GetTestRunTimeRange());
                var submitEvent = events.GetEvents<StorageRequestedEventDTO>().SingleOrDefault(e => e.RequestId.ToHex() == contract.PurchaseId);
                if (submitEvent == null)
                {
                    // We're too early.
                    throw new TimeoutException(nameof(GetContractOnChainSubmittedUtc) + "StorageRequest not found on-chain.");
                }
                return submitEvent.Block.Utc;
            }, nameof(GetContractOnChainSubmittedUtc));
        }

        private TestToken GetContractCostPerSlot(TestToken pricePerBytePerSecond, ByteSize slotSize, TimeSpan slotDuration)
        {
            var cost = pricePerBytePerSecond.TstWei * slotSize.SizeInBytes * (int)slotDuration.TotalSeconds;
            return cost.TstWei();
        }

        protected void AssertContractSlotsAreFilledByHosts(IStoragePurchaseContract contract, IPrometheiNodeGroup hosts, bool allowExtraSlotBlocks = false)
        {
            var fills = Array.Empty<SlotFill>();
            Time.Retry(() =>
            {
                fills = GetOnChainSlotFills(hosts, contract.PurchaseId);
                fills = KeepOnlyRecent(fills);
                if (fills.Length != contract.Purchase.PurchaseParams.Nodes) throw new Exception("Not all slots were filled...");
            }, nameof(AssertContractSlotsAreFilledByHosts));

            foreach (var f in fills)
            {
                AssertHostHoldsSlot(f, contract, allowExtraSlotBlocks);
            }
        }

        protected void AssertHostHoldsSlot(SlotFill f, IStoragePurchaseContract contract, bool allowExtras)
        {
            var slots = f.Host.Marketplace.GetSlots();
            var space = f.Host.Space();

            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(slots.Any(s => s.SlotIndex == (long)f.SlotFilledEvent.SlotIndex), Is.True);
            Assert.That(space.QuotaUsedBytes, Is.GreaterThanOrEqualTo(contract.Purchase.PurchaseParams.SlotSize.SizeInBytes));

            var manifest = f.Host.DownloadManifestOnly(contract.EncodedContentId);
            var expectedBlocks = CalculateSlotBlockIndices(
                blocksInDataset: manifest.Manifest.NumBlocks,
                numHosts: contract.Purchase.PurchaseParams.Nodes,
                slotIndex: (int)f.SlotFilledEvent.SlotIndex
            );

            AssertNodeHoldsDatasetBlocks(f.Host, contract.EncodedContentId, expectedBlocks, allowExtras);
        }

        protected IndexSet CalculateSlotBlockIndices(int blocksInDataset, int numHosts, int slotIndex)
        {
            if (slotIndex >= numHosts) throw new Exception("What?");
            var indexSet = new IndexSet();

            var numSlotBlocks = Int.DivUp(blocksInDataset, numHosts);

            var firstSlotBlockIndex = slotIndex * numSlotBlocks;
            var lastSlotBlockIndex = (slotIndex + 1) * numSlotBlocks;

            for (var i = 0; i < blocksInDataset; i++)
            {
                indexSet[i] = firstSlotBlockIndex <= i && i < lastSlotBlockIndex;
            }
            return indexSet;
        }

        protected void AssertContractIsOnChain(IStoragePurchaseContract contract)
        {
            Log("Check the creation event");
            AssertOnChainEvents(events =>
            {
                var onChainRequests = events.GetEvents<StorageRequestedEventDTO>();
                if (onChainRequests.Any(r => r.RequestId.ToHex() == contract.PurchaseId)) return;
                throw new Exception($"OnChain request {contract.PurchaseId} not found...");
            }, nameof(AssertContractIsOnChain));

            Log("Check that the getRequest call returns it");
            var rid = contract.PurchaseId.HexToByteArray();
            var cachedRequest = GetContracts().GetRequest(rid);
            if (cachedRequest == null) throw new Exception($"Failed to get Request from {nameof(GetRequestFunction)}");
            var r = cachedRequest.Request;
            Assert.That(r.Ask.Duration, Is.EqualTo(contract.Purchase.PurchaseParams.Duration.TotalSeconds));
            Assert.That(r.Ask.Slots, Is.EqualTo(contract.Purchase.PurchaseParams.Nodes));
            Assert.That(((int)r.Ask.ProofProbability), Is.EqualTo(contract.Purchase.PurchaseParams.ProofProbability));
        }

        protected void AssertOnChainEvents(Action<IPrometheiContractsEvents> onEvents, string description)
        {
            Time.Retry(() =>
            {
                var events = GetContracts().GetEvents(GetTestRunTimeRange());
                onEvents(events);
            }, description);
        }

        protected TimeSpan CalculateContractFailTimespan()
        {
            var config = GetContracts().Deployment.Config;
            var requiredNumMissedProofs = Convert.ToInt32(config.Collateral.MaxNumberOfSlashes);
            var periodDuration = GetPeriodDuration();
            var gracePeriod = periodDuration;

            // Each host could miss 1 proof per period,
            // so the time we should wait is period time * requiredNum of missed proofs.
            // Except: the proof requirement has a concept of "downtime":
            // a segment of time where proof is not required.
            // We calculate the probability of downtime and extend the waiting
            // timeframe by a factor, such that all hosts are highly likely to have 
            // failed a sufficient number of proofs.

            float n = requiredNumMissedProofs;
            return gracePeriod + periodDuration * n * GetDowntimeFactor(config);
        }

        private float GetDowntimeFactor(MarketplaceConfig config)
        {
            byte numBlocksInDowntimeSegment = config.Proofs.Downtime;
            float downtime = numBlocksInDowntimeSegment;
            float window = 256.0f;
            var chanceOfDowntime = downtime / window;
            return 1.0f + (5.0f * chanceOfDowntime);
        }

        public class SlotFill
        {
            public SlotFill(SlotFilledEventDTO slotFilledEvent, IPrometheiNode host)
            {
                SlotFilledEvent = slotFilledEvent;
                Host = host;
            }

            public SlotFilledEventDTO SlotFilledEvent { get; }
            public IPrometheiNode Host { get; }

            public override string ToString()
            {
                return SlotFilledEvent.ToString();
            }
        }

        private class MarketplaceHandle
        {
            public MarketplaceHandle(IGethNode geth, IPrometheiContracts contracts, ChainMonitor? chainMonitor)
            {
                Geth = geth;
                Contracts = contracts;
                ChainMonitor = chainMonitor;
            }

            public IGethNode Geth { get; }
            public IPrometheiContracts Contracts { get; }
            public ChainMonitor? ChainMonitor { get; }
        }
    }
}
