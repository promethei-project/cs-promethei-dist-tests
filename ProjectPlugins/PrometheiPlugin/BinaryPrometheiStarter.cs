using PrometheiClient;
using Core;
using Utils;
using System.Diagnostics;

namespace PrometheiPlugin
{
    public class BinaryPrometheiStarter : IPrometheiStarter
    {
        private readonly IPluginTools pluginTools;
        private readonly ProcessControlMap processControlMap;
        private readonly static NumberSource numberSource = new NumberSource(1);
        private readonly static FreePortFinder freePortFinder = new FreePortFinder();
        private readonly static object _lock = new object();
        private readonly static string dataParentDir = "promethei_disttest_datadirs";
        private readonly static PrometheiExePath prometheiExePath = new PrometheiExePath();

        static BinaryPrometheiStarter()
        {
            StopAllPrometheiProcesses();
            DeleteParentDataDir();
        }

        public BinaryPrometheiStarter(IPluginTools pluginTools, ProcessControlMap processControlMap)
        {
            this.pluginTools = pluginTools;
            this.processControlMap = processControlMap;
        }

        public IPrometheiInstance[] BringOnline(PrometheiSetup prometheiSetup)
        {
            lock (_lock)
            {
                LogSeparator();
                Log($"Starting {prometheiSetup.Describe()}...");

                return StartPrometheiBinaries(prometheiSetup, prometheiSetup.NumberOfNodes);
            }
        }

        public void Decommission()
        {
            lock (_lock)
            {
                processControlMap.StopAll();
            }
        }

        private IPrometheiInstance[] StartPrometheiBinaries(PrometheiStartupConfig startupConfig, int numberOfNodes)
        {
            var result = new List<IPrometheiInstance>();
            for (var i = 0; i < numberOfNodes; i++)
            {
                result.Add(StartBinary(startupConfig));
            }

            return result.ToArray();
        }

        private IPrometheiInstance StartBinary(PrometheiStartupConfig config)
        {
            var name = GetName(config);
            var dataDir = Path.Combine(dataParentDir, $"datadir_{numberSource.GetNextNumber()}");
            var pconfig = new PrometheiProcessConfig(name, freePortFinder, dataDir);
            Log(pconfig);

            var factory = new PrometheiProcessRecipe(pconfig, prometheiExePath);
            var recipe = factory.Initialize(config);

            var startInfo = new ProcessStartInfo(
                fileName: recipe.Cmd,
                arguments: recipe.Args
            );
            //startInfo.UseShellExecute = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            var process = Process.Start(startInfo);
            if (process == null || process.HasExited)
            {
                throw new Exception("Failed to start");
            }

            var local = "localhost";
            var instance = new PrometheiInstance(
                name: name,
                imageName: "binary",
                startUtc: DateTime.UtcNow,
                discoveryEndpoint: new Address("Disc", pconfig.LocalIpAddrs.ToString(), pconfig.DiscPort),
                apiEndpoint: new Address("Api", "http://" + local, pconfig.ApiPort),
                listenEndpoint: new Address("Listen", local, pconfig.ListenPort),
                ethAccount: null,
                metricsEndpoint: null
            );

            var pc = new BinaryProcessControl(pluginTools.GetLog(), process, pconfig);
            processControlMap.Add(instance, pc);

            return instance;
        }

        private string GetName(PrometheiStartupConfig config)
        {
            if (!string.IsNullOrEmpty(config.NameOverride))
            {
                return config.NameOverride + "_" + numberSource.GetNextNumber();
            }
            return "promethei_" + numberSource.GetNextNumber();
        }

        private void LogSeparator()
        {
            Log("----------------------------------------------------------------------------");
        }

        private void Log(PrometheiProcessConfig pconfig)
        {
            Log(
                "NodeConfig:Name=" + pconfig.Name +
                "ApiPort=" + pconfig.ApiPort +
                "DiscPort=" + pconfig.DiscPort +
                "ListenPort=" + pconfig.ListenPort +
                "DataDir=" + pconfig.DataDir
            );
        }

        private void Log(string message)
        {
            pluginTools.GetLog().Log(message);
        }

        private static void DeleteParentDataDir()
        {
            if (Directory.Exists(dataParentDir))
            {
                Directory.Delete(dataParentDir, true);
            }
        }

        private static void StopAllPrometheiProcesses()
        {
            var processes = Process.GetProcesses();
            var prometheies = processes.Where(p =>
                p.ProcessName.ToLowerInvariant() == "promethei" &&
                p.MainModule != null &&
                p.MainModule.FileName == prometheiExePath.Get()
            ).ToArray();

            foreach (var c in prometheies)
            {
                c.Kill();
                c.WaitForExit();
            }
        }
    }
}
