using PrometheiContractsPlugin.Marketplace;
using Core;
using GethPlugin;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;
using Logging;
using Newtonsoft.Json;
using Utils;

namespace PrometheiContractsPlugin
{
    public class PrometheiContractsStarter
    {
        private readonly IPluginTools tools;

        public PrometheiContractsStarter(IPluginTools tools)
        {
            this.tools = tools;
        }

        public PrometheiContractsDeployment Deploy(CoreInterface ci, Action<IPrometheiContractsSetup> setupBuilder)
        {
            Log("Starting Promethei SmartContracts container...");
            var setup = CreateSetup(setupBuilder);

            var workflow = tools.CreateWorkflow();
            var startupConfig = CreateStartupConfig(setup);
            startupConfig.NameOverride = "promethei-contracts";

            var recipe = new PrometheiContractsContainerRecipe(setup.InfoVersion);
            Log($"Using image: {recipe.Image}");

            var containers = workflow.Start(1, recipe, startupConfig).WaitForOnline();
            if (containers.Containers.Length != 1) throw new InvalidOperationException("Expected 1 Promethei contracts container to be created. Test infra failure.");
            var container = containers.Containers[0];

            Log("Container started.");
            var watcher = workflow.CreateCrashWatcher(container);
            watcher.Start();

            try
            {
                var result = DeployContract(container, workflow, setup);

                workflow.Stop(containers, waitTillStopped: false);
                watcher.Stop();
                Log("Container stopped.");
                return result;
            }
            catch (Exception ex)
            {
                Log("Failed to deploy contract: " + ex);
                Log("Downloading Promethei SmartContracts container log...");
                ci.DownloadLog(container);
                throw;
            }
        }

        public IPrometheiContracts Wrap(PrometheiContractsDeployment deployment, Action<IPrometheiContractsSetup> setupBuilder)
        {
            var setup = CreateSetup(setupBuilder);
            return new PrometheiContractsAccess(tools.GetLog(), setup.RpcNode, deployment, setup.RequestsCache);
        }

        private PrometheiContractsDeployment DeployContract(RunningContainer container, IStartupWorkflow workflow, PrometheiContractsSetup setup)
        {
            Log("Deploying SmartContract...");
            WaitUntil(() =>
            {
                var logHandler = new ContractsReadyLogHandler(tools.GetLog());
                workflow.DownloadContainerLog(container, logHandler, 100);
                return logHandler.Found;
            }, nameof(DeployContract));
            Log("Contracts deployed. Extracting addresses...");

            var extractor = new ContractsContainerInfoExtractor(tools.GetLog(), workflow, container);
            var marketplaceAddress = extractor.ExtractMarketplaceAddress();
            var (abi, bytecode) = extractor.ExtractMarketplaceAbiAndByteCode();
            if (string.IsNullOrEmpty(abi)) throw new Exception("ABI not received.");
            if (string.IsNullOrEmpty(bytecode)) throw new Exception("bytecode not received.");
            EnsureCompatbility(abi, bytecode);

            var interaction = new ContractInteractions(tools.GetLog(), setup.RpcNode);
            var tokenAddress = interaction.GetTokenAddress(marketplaceAddress);
            Log("TokenAddress: " + tokenAddress);

            Log("Extract completed. Checking sync...");

            Time.WaitUntil(() => interaction.IsSynced(marketplaceAddress, abi), nameof(DeployContract));

            Log("Synced. Promethei SmartContracts deployed. Getting configuration...");

            var config = GetMarketplaceConfiguration(marketplaceAddress, setup.RpcNode);
            Log("Got config: " + JsonConvert.SerializeObject(config));

            ConfigShouldEqual(config.Proofs.Period, PrometheiContractsContainerRecipe.PeriodSeconds, "Period");
            ConfigShouldEqual(config.Proofs.Timeout, PrometheiContractsContainerRecipe.TimeoutSeconds, "Timeout");
            ConfigShouldEqual(config.Proofs.Downtime, PrometheiContractsContainerRecipe.DowntimeSeconds, "Downtime");
            if (setup.MaxReservationsOverride.HasValue)
            {
                ConfigShouldEqual(config.Reservations.MaxReservations, setup.MaxReservationsOverride.Value, "Max reservations override");
            }

            return new PrometheiContractsDeployment(config, marketplaceAddress, abi);
        }

        private void ConfigShouldEqual(ulong value, int expected, string name)
        {
            if (Convert.ToInt32(value) != expected)
            {
                throw new Exception($"Config value '{name}' should be deployed as '{expected}' but was '{value}'");
            }
            Log($"Config value '{name}' correctly deployed as '{value}'");
        }

        private MarketplaceConfig GetMarketplaceConfiguration(ContractAddress marketplaceAddress, IGethNode gethNode)
        {
            var func = new ConfigurationFunctionBase();
            var response = gethNode.Call<ConfigurationFunctionBase, ConfigurationOutputDTO>(marketplaceAddress, func);
            return response.ReturnValue1;
        }

        private void EnsureCompatbility(string abi, string bytecode)
        {
            var expectedByteCode = MarketplaceDeploymentBase.BYTECODE.ToLowerInvariant();

            if (bytecode != expectedByteCode)
            {
                Log("Deployed contract is incompatible with current build of PrometheiContracts plugin. Running self-updater...");
                var selfUpdater = new SelfUpdater();
                selfUpdater.Update(abi, bytecode);
            }
        }

        private PrometheiContractsSetup CreateSetup(Action<IPrometheiContractsSetup> createSetup)
        {
            var builder = new PrometheiContractsSetupBuilder();
            createSetup(builder);
            return builder.Build();
        }

        private void Log(string msg)
        {
            tools.GetLog().Log(msg);
        }

        private void WaitUntil(Func<bool> predicate, string msg)
        {
            var duration = Time.WaitUntil(predicate, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2), msg);
            Log($"{msg} {Time.FormatDuration(duration)}");
        }

        private StartupConfig CreateStartupConfig(PrometheiContractsSetup setup)
        {
            var startupConfig = new StartupConfig();
            startupConfig.Add(setup);
            return startupConfig;
        }
    }

    public class ContractsReadyLogHandler : LogHandler
    {
        // Log should contain 'Compiled 15 Solidity files successfully' at some point.
        private const string RequiredCompiledString = "Solidity files successfully";
        // When script is done, it prints the ready-string.
        private const string ReadyString = "Done! Sleeping indefinitely...";
        private readonly ILog log;

        public ContractsReadyLogHandler(ILog log)
        {
            this.log = log;

            log.Debug($"Looking for '{RequiredCompiledString}' and '{ReadyString}' in container logs...");
        }

        public bool SeenCompileString { get; private set; }
        public bool Found { get; private set; }

        protected override void ProcessLine(string line)
        {
            log.Debug(line);
            if (line.Contains(RequiredCompiledString)) SeenCompileString = true;
            if (line.Contains(ReadyString))
            {
                if (!SeenCompileString) throw new Exception("PrometheiContracts deployment failed. " +
                    "Solidity files not compiled before process exited.");

                Found = true;
            }
        }
    }
}
