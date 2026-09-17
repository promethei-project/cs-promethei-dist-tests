using PrometheiClient;
using PrometheiNetworkConfig;
using ArgsUniform;
using FileUtils;
using Logging;
using MetricsServer;
using Utils;

namespace EphemeralClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var uniformArgs = new ArgsUniform<Configuration>(args);
            var config = uniformArgs.Parse(true);

            var p = new Program(config);
            p.Run();
        }

        private readonly TimeSpan loopDelay = TimeSpan.FromMinutes(30);

        private readonly ILog log;
        private readonly MetricsServer.MetricsServer metricsServer;
        private readonly LocalNode localNode;
        private readonly GatewayClient.GatewayClient gateway;
        private readonly MetricsEvent heartbeat;
        private readonly MetricsEvent failedToDownload;
        private readonly MetricsGauge downloadSpeed;
        private readonly IFileManager fileManager;
        private readonly string downloadFolder = "temp_downloadfiles";
        private readonly Configuration config;

        public Program(Configuration config)
        {
            log = new TimestampPrefixer(
                new LogSplitter(
                    new FileLog(Path.Combine("logs", "autoclient")),
                    new ConsoleLog()
                )
            );

            Log("Ephemeral client");

            metricsServer = new MetricsServer.MetricsServer(log, config.MetricsPort, "gateway_tester");
            metricsServer.Start();

            heartbeat = metricsServer.CreateEvent("heartbeat", "application is alive");
            failedToDownload = metricsServer.CreateEvent("failure", "gateway download failed");
            downloadSpeed = metricsServer.CreateGauge("download_speed", "bytes per second");

            localNode = new LocalNode(log, metricsServer);

            Log("Loading network config...");
            var networkConnector = new PrometheiNetworkConnector(log);
            var network = networkConnector.GetConfig();
            Log($"Network: {network.Name}");
            gateway = new GatewayClient.GatewayClient(network);

            Log("Preparing local node...");
            localNode.Initialize(network);

            fileManager = new FileManager(log, "temp_uploadfiles");
            this.config = config;
        }

        private void Run()
        {
            Log("Run: start");

            Sleep(10);

            while (true)
            {
                heartbeat.Now();

                RunCheck();

                Sleep(loopDelay);
            }
        }

        private void RunCheck()
        {
            try
            {
                RunWithNode();
                Log("Check: Success");
            }
            catch (Exception ex)
            {
                log.Error($"Check: Exception: {ex}");
            }
        }

        private void RunWithNode()
        {
            var node = localNode.Start();
            Directory.CreateDirectory(downloadFolder);

            try
            {
                fileManager.ScopedFiles(() =>
                {
                    RunCheckSteps(node);
                });
            }
            finally
            {
                Directory.Delete(downloadFolder, true);
                localNode.StopAndClean();
            }
        }

        private void RunCheckSteps(IPrometheiNode node)
        {
            var file = fileManager.GenerateFile(config.FilesizeMb.MB());

            var cid = new ContentId();
            var uploadTime = Stopwatch.Measure(log, nameof(node.UploadFile), () =>
            {
                cid = node.UploadFile(file);
            });

            var downloaded = DownloadFileFromGateway(cid);
            try
            {
                file.AssertIsEqual(downloaded);
            }
            catch (Exception ex)
            {
                log.Error($"Downloaded file was not equal to uploaded file: {ex}");
            }
        }

        private TrackedFile DownloadFileFromGateway(ContentId cid)
        {
            try
            {
                var filename = Path.Combine(downloadFolder, Guid.NewGuid().ToString());
                var downloadTime = Stopwatch.Measure(log, nameof(gateway.Download), () =>
                {
                    using var fileStream = File.OpenWrite(filename);
                    {
                        var stream = gateway.Download(cid.Id);
                        stream.CopyTo(fileStream);
                    }
                });

                var downloadSeconds = downloadTime.TotalSeconds;
                double downloadBytes = config.FilesizeMb.MB().SizeInBytes;
                var bytesPerSecond = downloadBytes / downloadSeconds;
                downloadSpeed.Set(Convert.ToInt32(Math.Round(bytesPerSecond)));

                return TrackedFile.FromPath(log, filename);
            }
            catch
            {
                failedToDownload.Now();
                throw;
            }
        }

        private static void Sleep(TimeSpan span)
        {
            Thread.Sleep(span);
        }

        private static void Sleep(int sec)
        {
            Sleep(TimeSpan.FromSeconds(sec));
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }
}
