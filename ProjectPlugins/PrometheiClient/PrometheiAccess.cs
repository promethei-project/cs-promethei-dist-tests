using PrometheiClient;
using PrometheiOpenApi;
using Logging;
using Newtonsoft.Json;
using Utils;
using WebUtils;

namespace PrometheiClient
{
    public class PrometheiAccess
    {
        private readonly ILog log;
        private readonly IHttpFactory httpFactory;
        private readonly IProcessControl processControl;
        private readonly IPrometheiInstance instance;
        private readonly Mapper mapper = new Mapper();

        public PrometheiAccess(ILog log, IHttpFactory httpFactory, IProcessControl processControl, IPrometheiInstance instance)
        {
            this.log = log;
            this.httpFactory = httpFactory;
            this.processControl = processControl;
            this.instance = instance;
        }

        public void Stop(bool waitTillStopped)
        {
            processControl.Stop(waitTillStopped);
        }

        public void Restart()
        {
            DownloadLog("_before_restart");
            processControl.Restart();
        }

        public IDownloadedLog DownloadLog(string additionalName = "")
        {
            var file = log.CreateSubfile(GetName() + additionalName);
            Log($"Downloading logs to '{file.Filename}'");
            return processControl.DownloadLog(file);
        }

        public string GetImageName()
        {
            return instance.ImageName;
        }

        public DateTime GetStartUtc()
        {
            return instance.StartUtc;
        }

        public DebugInfo GetDebugInfo()
        {
            return mapper.Map(OnPromethei(api => api.GetDebugInfoAsync()));
        }

        public void SetLogLevel(string logLevel)
        {
            try
            {
                OnPromethei(async api =>
                {
                    await api.SetDebugLogLevelAsync(logLevel);
                    return string.Empty;
                });
            }
            catch (Exception exc)
            {
                log.Error("Failed to set log level: " + exc);
            }
        }

        public string GetSpr()
        {
            return CrashCheck(() =>
            {
                var endpoint = GetEndpoint();
                var json = endpoint.HttpGetString("spr");
                var response = JsonConvert.DeserializeObject<SprResponse>(json);
                return response!.Spr;
            });
        }

        private class SprResponse
        {
            public string Spr { get; set; } = string.Empty;
        }

        public DebugPeer GetDebugPeer(string peerId)
        {
            try
            {
                var response = OnPromethei(api => api.GetDebugPeerAsync(peerId));
                return mapper.Map(response);
            }
            catch
            {
                return new DebugPeer
                {
                    PeerId = peerId,
                    IsPeerFound = false,
                    Addresses = Array.Empty<string>()
                };
            }

        }

        public void SetSystemTestingOption(string key, string value)
        {
            OnPromethei(async api =>
            {
                await api.SetSTOAsync(key, value);
                return string.Empty;
            });
        }

        public void ConnectToPeer(string peerId, string[] peerMultiAddresses)
        {
            OnPromethei(api =>
            {
                Time.Wait(api.ConnectPeerAsync(peerId, peerMultiAddresses));
                return Task.FromResult(string.Empty);
            });
        }

        public string UploadFile(UploadInput uploadInput)
        {
            return OnPromethei(api =>
            {
                // What we have here is, the inability of the generated code to let us control the
                // content headers of the request. We have to use partial-class customizations to modify
                // the default behavior. My god have mercy on us all.
                api.SetNextUploadInput(uploadInput);

                return api.UploadAsync(uploadInput.ContentType, uploadInput.ContentDisposition, uploadInput.FileStream);
            });
        }

        public Stream DownloadFile(string contentId)
        {
            var fileResponse = OnPrometheiNoRetry(api => api.DownloadNetworkStreamAsync(contentId));
            if (fileResponse.StatusCode != 200) throw new Exception("Download failed with StatusCode: " + fileResponse.StatusCode);
            return fileResponse.Stream;
        }

        public async Task DownloadFileAsync(string contentId, Stream destination, CancellationToken cancellationToken)
        {
            var address = GetAddress();
            var url = $"{address.Host}:{address.Port}/api/promethei/v1/data/{Uri.EscapeDataString(contentId)}/network/stream";

            using var client = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Download failed with StatusCode: {response.StatusCode}");

            // Measure time-to-first-byte (TTFB) of the response body: this is
            // when stream index 0 becomes readable to the client.
            var ttfbWatch = System.Diagnostics.Stopwatch.StartNew();
            using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[81920];
            var firstRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            Log($"{GetName()} TTFB {ttfbWatch.Elapsed.TotalMilliseconds:F0} ms");
            if (firstRead > 0) await destination.WriteAsync(buffer.AsMemory(0, firstRead), cancellationToken);
            await source.CopyToAsync(destination, cancellationToken);
        }

        public LocalDataset DownloadStreamless(ContentId cid)
        {
            var response = OnPromethei(api => api.DownloadNetworkAsync(cid.Id));
            return mapper.Map(response);
        }

        public LocalDataset DownloadManifestOnly(ContentId cid)
        {
            var response = OnPromethei(api => api.DownloadNetworkManifestAsync(cid.Id));
            return mapper.Map(response);
        }

        public LocalDatasetList LocalFiles()
        {
            return mapper.Map(OnPromethei(api => api.ListDataAsync()));
        }

        public DatasetStatus GetDatasetStatus(ContentId cid)
        {
            var raw = OnPromethei(api => api.GetDatasetStatusAsync(cid.Id));
            return mapper.Map(raw, cid);
        }

        public void SalesAvailability(CreateStorageAvailability request)
        {
            var body = mapper.Map(request);
            OnPromethei(api =>
            {
                api.OfferStorageAsync(body).Wait();
                return Task.FromResult(string.Empty);
            });
        }

        public StorageAvailability GetAvailability()
        {
            var collection = OnPromethei(api => api.GetAvailabilityAsync());
            return mapper.Map(collection);
        }

        public StorageSlotItem[] GetSlots()
        {
            var slotIds = OnPromethei(api => api.GetActiveSlotsAsync());
            return mapper.Map(slotIds, id => OnPromethei(api => api.GetActiveSlotByIdAsync(id)));
        }

        public StorageSlotItem GetSlot(string slotId)
        {
            var slot = OnPromethei(api => api.GetActiveSlotByIdAsync(slotId));
            if (slot == null) throw new Exception($"Unable to find slot by Id: '{slotId}'");
            return mapper.Map(slot, slotId);
        }

        public string RequestStorage(StoragePurchaseRequest request)
        {
            var body = mapper.Map(request);
            return OnPromethei(api => api.CreateStorageRequestAsync(request.Cid.Id, body));
        }

        public PrometheiSpace Space()
        {
            var space = OnPromethei(api => api.SpaceAsync());
            return mapper.Map(space);
        }

        public StoragePurchase? GetPurchaseStatus(string purchaseId)
        {
            var purchase = OnPromethei(api => api.GetPurchaseAsync(purchaseId));
            return mapper.Map(purchase);
        }

        public string[] GetPurchases()
        {
            return OnPromethei(api => api.GetPurchasesAsync())
                .Select(p => p.ToLowerInvariant())
                .ToArray();
        }

        public string GetName()
        {
            return instance.Name;
        }

        public Address GetDiscoveryEndpoint()
        {
            return instance.DiscoveryEndpoint;
        }

        public Address GetApiEndpoint()
        {
            return instance.ApiEndpoint;
        }

        public Address GetListenEndpoint()
        {
            return instance.ListenEndpoint;
        }

        public bool HasCrashed()
        {
            return processControl.HasCrashed();
        }

        public Address? GetMetricsEndpoint()
        {
            return instance.MetricsEndpoint;
        }

        public EthAccount? GetEthAccount()
        {
            return instance.EthAccount;
        }

        public void DeleteDataDirFolder()
        {
            processControl.DeleteDataDirFolder();
        }

        private T OnPrometheiNoRetry<T>(Func<PrometheiApiClient, Task<T>> action)
        {
            var timeSet = httpFactory.WebCallTimeSet;
            var noRetry = new Retry(nameof(OnPrometheiNoRetry),
                maxTimeout: TimeSpan.FromSeconds(1.0),
                sleepAfterFail: TimeSpan.FromSeconds(2.0),
                onFail: f => { },
                failFast: true);

            var result = httpFactory.CreateHttp(GetHttpId(), h => CheckPrometheiCrashed()).OnClient(client => CallPromethei(client, action), noRetry);
            return result;
        }

        private T OnPromethei<T>(Func<PrometheiApiClient, Task<T>> action)
        {
            var result = httpFactory.CreateHttp(GetHttpId(), h => CheckPrometheiCrashed()).OnClient(client => CallPromethei(client, action));
            return result;
        }

        private T CallPromethei<T>(HttpClient client, Func<PrometheiApiClient, Task<T>> action)
        {
            CheckPrometheiCrashed();

            var address = GetAddress();
            var api = new PrometheiApiClient(client);
            api.BaseUrl = $"{address.Host}:{address.Port}/api/promethei/v1";
            try
            {
                return CrashCheck(() => Time.Wait(action(api)));
            }
            catch (Exception ex)
            {
                throw new Exception($"Call to {GetName()} threw:", ex);
            }
        }

        private T CrashCheck<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            finally
            {
                CheckPrometheiCrashed();
            }
        }

        private IEndpoint GetEndpoint()
        {
            return httpFactory
                .CreateHttp(GetHttpId(), h => CheckPrometheiCrashed())
                .CreateEndpoint(GetAddress(), "/api/promethei/v1/", GetName());
        }

        private Address GetAddress()
        {
            return instance.ApiEndpoint;
        }

        private string GetHttpId()
        {
            return GetAddress().ToString();
        }

        private void CheckPrometheiCrashed()
        {
            if (processControl.HasCrashed()) throw new Exception($"Promethei {GetName()} has crashed.");
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }

    public class UploadInput
    {
        public UploadInput(string contentType, string contentDisposition, FileStream fileStream)
        {
            ContentType = contentType;
            ContentDisposition = contentDisposition;
            FileStream = fileStream;
        }

        public string ContentType { get; }
        public string ContentDisposition { get; }
        public FileStream FileStream { get; }
    }
}

namespace PrometheiOpenApi
{
    public partial class PrometheiApiClient
    {
        private UploadInput? uploadInput;

        public void SetNextUploadInput(UploadInput input)
        {
            uploadInput = input;
        }

        partial void PrepareRequest(HttpClient client, HttpRequestMessage request, System.Text.StringBuilder urlBuilder)
        {
            if (request == null) return;
            if (request.Content == null) return;
            if (uploadInput == null) return;

            request.Content.Headers.Remove("Content-Type");
            request.Content.Headers.Add("Content-Type", uploadInput.ContentType);
            request.Content.Headers.Add("Content-Disposition", uploadInput.ContentDisposition);

            // The httpclient lock in WebUtils.Http protects us from a race condition here.
            uploadInput = null;
        }
    }
}