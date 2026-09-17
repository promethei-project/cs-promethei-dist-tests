using PrometheiClient;
using PrometheiContractsPlugin.Marketplace;
using PrometheiPlugin;
using PrometheiReleaseTests.Utils;
using GethPlugin;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using NUnit.Framework;
using Utils;

namespace PrometheiReleaseTests.MarketTests
{
    [TestFixture(5)]
    public class DeceptiveContractTest : MarketplaceAutoBootstrapDistTest
    {
        private readonly PurchaseParams largePurchaseParams;
        private readonly int numberOfDeceptiveRequests;
        private readonly List<string> deceptiveRequestIds = new List<string>();

        public DeceptiveContractTest(int numberOfDeceptiveRequests)
        {
            this.numberOfDeceptiveRequests = numberOfDeceptiveRequests;

            largePurchaseParams = PurchaseParams.Default.WithUploadFilesize(100.MB());
        }

        protected override int NumberOfHosts => 5;
        protected override int NumberOfClients => 1;
        protected override bool MonitorProofPeriods => false;

        private (Request, IPrometheiNode) PrepareLargeDatasetAndRequest()
        {
            Log($"Creating a storage request with {largePurchaseParams.SlotSize} slots, copying the on-chain request and waiting until it expires...");
            var client = StartClients().Single();
            var cid = client.UploadFile(GenerateTestFile(largePurchaseParams.UploadFilesize));
            var clientRequest = client.Marketplace.RequestStorage(new StoragePurchaseRequest(cid,
                // No host will pick up a request with this collateral requirement:
                p => p.WithCollateralPerByte(DefaultAvailabilityMaxCollateralPerByte + 10.Tst()))
                // The purpose of this call is to:
                // A) Get the large dataset encoded/verifiable and available in the client node
                // 2) Get the request from chain, so we can manipulate it and post malicious copies.
            );

            clientRequest.WaitForStorageContractSubmitted();
            // We break the test code abstraction here to get and post a request on-chain directly.
            var request = Time.Retry(() => GetRawRequest(clientRequest.PurchaseId), nameof(GetRawRequest));
            
            Log($"On-chain request has slotSize {request.Ask.SlotSize} ({new ByteSize(Convert.ToInt64(request.Ask.SlotSize))})");
            return (request, client);
        }

        [Test]
        public void HostQuotaTooSmallForRealSlotSize()
        {
            var (request, client) = PrepareLargeDatasetAndRequest();
            var deceptiveRequestSlotSize = largePurchaseParams.SlotSize.Multiply(0.1);
            var hostQuotaSize = deceptiveRequestSlotSize.Multiply(1.1);

            Assert.That(hostQuotaSize.SizeInBytes, Is.LessThan(largePurchaseParams.SlotSize.SizeInBytes));

            Log($"Starting hosts with quota size: {hostQuotaSize}");
            var hosts = StartHosts(s => s.WithStorageQuota(hostQuotaSize));

            Log($"Creating {numberOfDeceptiveRequests} deceptive request(s) for the same data.");
            Log($"Real slotSize: {largePurchaseParams.SlotSize}");
            Log($"Deceptive request slotSize: {deceptiveRequestSlotSize}");
            Log("The slotSize in the request will trick the host into thinking it can store a full slot.");
            Log("But the real slotSize is too large for the host quota, so it can't. So it should discard this request.");

            PostDeceptiveRequests(client, request, deceptiveRequestSlotSize);

            AssertHostsIgnoreDeceptiveRequest(hosts);

            CreateAndStartLegitRequest(client, hostQuotaSize);
        }

        [Test]
        public void HostQuotaIsLargeEnoughForRealSlotSizeButDeceptiveRequestIsRejectedAnyway()
        {
            var (request, client) = PrepareLargeDatasetAndRequest();
            var deceptiveRequestSlotSize = largePurchaseParams.SlotSize.Multiply(0.1);
            var hostQuotaSize = largePurchaseParams.SlotSize.Multiply(largePurchaseParams.Nodes * (numberOfDeceptiveRequests + 1));

            Assert.That(hostQuotaSize.SizeInBytes, Is.GreaterThan(largePurchaseParams.SlotSize.SizeInBytes * numberOfDeceptiveRequests));

            Log($"Starting hosts with quota size: {hostQuotaSize}");
            var hosts = StartHosts(s => s.WithStorageQuota(hostQuotaSize));

            Log($"Creating {numberOfDeceptiveRequests} deceptive request(s) for the same data.");
            Log($"Real slotSize: {largePurchaseParams.SlotSize}");
            Log($"Deceptive request slotSize: {deceptiveRequestSlotSize}");
            Log("The slotSize in the request is under-reporting the real slotSize of the dataset.");
            Log("The hosts have enough capacity to fill the slots and service the request, but.");
            Log("They will be paid for the deceptive, small slotSize, while storing and proving");
            Log("the large slotSizes. The hosts should not fall for such trickery! and reject the request.");

            PostDeceptiveRequests(client, request, deceptiveRequestSlotSize);

            AssertHostsIgnoreDeceptiveRequest(hosts);

            CreateAndStartLegitRequest(client, hostQuotaSize);
        }

        private void PostDeceptiveRequests(IPrometheiNode client, Request request, ByteSize deceptiveRequestSlotSize)
        {
            request.Ask.SlotSize = Convert.ToUInt64(deceptiveRequestSlotSize.SizeInBytes);
            request.Ask.CollateralPerByte = 1;
            request.Ask.Duration = Convert.ToUInt64(HostAvailabilityMaxDuration.TotalSeconds * 0.95);
            PostRawRequests(numberOfDeceptiveRequests, request, client.EthAccount);

            ReadDeceptiveRequestIdsFromChain(request);
        }

        private void ReadDeceptiveRequestIdsFromChain(Request request)
        {
            var timeout = DateTime.UtcNow + TimeSpan.FromMinutes(5.0);
            while (deceptiveRequestIds.Count < numberOfDeceptiveRequests)
            {
                if (DateTime.UtcNow > timeout) Assert.Fail("Timed out waiting for deceptive requests to appear on-chain.");

                var requests = GetChainMonitor().Requests.ToArray();
                // we can recognize the deceptive ones by the slotSiz, collateral, and duration set above.
                var matches = requests.Where(r =>
                    Convert.ToUInt64(r.Ask.SlotSize.SizeInBytes) == request.Ask.SlotSize &&
                    r.Ask.CollateralPerByte.TstWei == 1 &&
                    Convert.ToUInt64(r.Ask.Duration.TotalSeconds) == request.Ask.Duration
                ).ToArray();
                foreach (var match in matches)
                {
                    if (!deceptiveRequestIds.Contains(match.Id)) deceptiveRequestIds.Add(match.Id);
                }
            }

            Log("Deceptive request IDs:");
            foreach (var id in deceptiveRequestIds) Log(id);
        }

        private void AssertHostsIgnoreDeceptiveRequest(IPrometheiNodeGroup hosts)
        {
            Assert.That(deceptiveRequestIds.Count, Is.GreaterThan(0));

            Log("Hosts will detect the deceptive contracts. " +
                "When they download the manifest, they should know something's wrong and disregard the request. " +
                "We check that they don't fill any deceptive contract slots for the duration of the request expiry.");

            WaitAndCheck(PurchaseParams.Default.Expiry, TimeSpan.FromSeconds(30), () =>
            {
                foreach (var id in deceptiveRequestIds)
                {
                    AssertNoSlotFills(hosts, id);
                    AssertHostsDidntDownloadDataset(hosts);
                }
            });

            Log("The hosts should be empty.");
            AssertHostsAreEmpty(hosts);
        }

        private void AssertHostsDidntDownloadDataset(IPrometheiNodeGroup hosts)
        {
            foreach (var host in hosts)
            {
                AssertHostDidntDownloadDataset(host);
            }
        }

        private void AssertHostDidntDownloadDataset(IPrometheiNode host)
        {
            // We expect the host to download a manifest blocks, and we expect
            // some clutter from the previous request.
            var numberOfManifestBlocks = numberOfDeceptiveRequests;
            var tolerance = 2 + numberOfDeceptiveRequests;

            var storedBlocks = host.Space().TotalBlocks;
            Assert.That(storedBlocks, Is.LessThan(numberOfManifestBlocks + tolerance),
                $"Host {host.GetName()} is storing more blocks than expected. " +
                $"Expected number of manifest blocks: {numberOfManifestBlocks} " +
                $"Tolerance: {tolerance} " +
                $"Stored blocks: {storedBlocks}");
        }

        private void AssertNoSlotFills(IPrometheiNodeGroup hosts, string id)
        {
            var fills = GetOnChainSlotFills(hosts, id);
            if (fills.Length > 0)
            {
                Assert.Fail(
                    $"Slot filled events for deceptive request detected: {string.Join(" ", fills.Select(f => f.ToString()).ToArray())}"
                );
            }
        }

        private void CreateAndStartLegitRequest(IPrometheiNode client, ByteSize hostQuotaSize)
        {
            Log("Now we create a legit request. The hosts should pick it up and the request should start.");
            var filesize = 2.MB();
            Assert.That(filesize.SizeInBytes, Is.LessThan(hostQuotaSize.Multiply(0.5).SizeInBytes),
                "Test misconfigured: Legit request does not fit in host quota.");

            var legitRequest = client.Marketplace
                .RequestStorage(new StoragePurchaseRequest(
                    client.UploadFile(GenerateTestFile(filesize))
                ));

            legitRequest.WaitForStorageContractStarted();
        }

        private void PostRawRequests(int number, Request request, EthAccount clientAccount)
        {
            var geth = GetGeth().WithDifferentAccount(clientAccount);
            var contracts = GetContracts().WithDifferentGeth(geth);
            var marketplaceAddress = contracts.Deployment.MarketplaceAddress;

            contracts.ApproveTestTokens(marketplaceAddress, (StartingBalanceTST / 2).Tst());

            var remaining = number;
            var timeout = DateTime.UtcNow + TimeSpan.FromMinutes(10);
            while (remaining > 0)
            {
                if (DateTime.UtcNow > timeout) Assert.Fail("Failed to post requests within 10 minutes");

                Log(JsonConvert.SerializeObject(request));

                Time.Retry(() =>
                {
                    CreateRequest(geth, marketplaceAddress, request);
                    request.Expiry++; // We must modify the request. Identical requests are ignored by the contract.
                    remaining--;
                }, nameof(CreateRequest));
            }
        }

        private void CreateRequest(IGethNode geth, ContractAddress marketplaceAddress, Request request)
        {
            var func = new RequestStorageFunction
            {
                Request = request
            };

            geth.SendTransaction(marketplaceAddress, func);
        }

        private Request GetRawRequest(string purchaseId)
        {
            var func = new GetRequestFunction
            {
                RequestId = purchaseId.HexToByteArray()
            };

            var request = GetGeth().Call<GetRequestFunction, GetRequestOutputDTO>(GetContracts().Deployment.MarketplaceAddress, func);
            return request.ReturnValue1;
        }
    }
}
