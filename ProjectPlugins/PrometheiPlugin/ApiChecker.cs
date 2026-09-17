using Core;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;
using Logging;
using System.Security.Cryptography;
using System.Text;
using Utils;

namespace PrometheiPlugin
{
    public class ApiChecker
    {
        // <INSERT-OPENAPI-YAML-HASH>
        private const string OpenApiYamlHash = "73-79-3C-43-2F-D8-2B-80-8E-7F-B7-17-CE-CA-DD-CB-0D-04-C6-DB-00-E0-C1-E7-1B-DD-05-46-08-D8-B8-80";
        private const string OpenApiFilePath = "/promethei/openapi.yaml";
        private const string DisableEnvironmentVariable = "PROMETHEIPLUGIN_DISABLE_APICHECK";

        private const string ExpectedSystemTestingOptionsWarning = "This application was compiled with system testing options enabled.";

        private const bool Disable = false;

        private const string Failure =
            "Promethei API compatibility check failed! " +
            "openapi.yaml used by PrometheiPlugin does not match openapi.yaml in Promethei container. The openapi.yaml in " +
            "'ProjectPlugins/PrometheiPlugin' has been overwritten with the container one. " +
            "Please and rebuild this project. If you wish to disable API compatibility checking, please set " +
            $"the environment variable '{DisableEnvironmentVariable}' or set the disable bool in 'ProjectPlugins/PrometheiPlugin/ApiChecker.cs'.";

        private static bool checkPassed = false;

        private readonly IPluginTools pluginTools;
        private readonly ILog log;

        public ApiChecker(IPluginTools pluginTools)
        {
            this.pluginTools = pluginTools;
            log = pluginTools.GetLog();

            if (string.IsNullOrEmpty(OpenApiYamlHash)) throw new Exception("OpenAPI yaml hash was not inserted by pre-build trigger.");
        }

        public void CheckCompatibility(RunningPod[] containers)
        {
            if (checkPassed) return;

            Log("PrometheiPlugin is checking API compatibility...");

            if (Disable || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DisableEnvironmentVariable)))
            {
                Log("API compatibility checking has been disabled.");
                checkPassed = true;
                return;
            }

            var workflow = pluginTools.CreateWorkflow();
            var container = containers.First().Containers.First();


            if (!IsContainerApiCompatible(workflow, container)) Fail();
            if (!IsCompiledWithSystemTestingOptions(workflow, container)) Fail();

            checkPassed = true;
        }

        private bool IsCompiledWithSystemTestingOptions(IStartupWorkflow workflow, RunningContainer container)
        {
            var log = workflow.DownloadContainerLog(container);
            var lines = log.GetLinesContaining(ExpectedSystemTestingOptionsWarning);
            if (lines.Any())
            {
                Log("System testing options confirmed.");
                return true;
            }

            Log("Application was not compiled with system testing options.");
            return false;
        }

        private bool IsContainerApiCompatible(IStartupWorkflow workflow, RunningContainer container)
        {
            var containerApi = workflow.ExecuteCommand(container, "cat", OpenApiFilePath);
            if (string.IsNullOrEmpty(containerApi)) return false;

            var containerHash = Hash(containerApi);
            if (containerHash != OpenApiYamlHash)
            {
                OverwriteOpenApiYaml(containerApi);
                return false;
            }

            Log("Container API compatibility check passed.");
            return true;
        }

        private void Fail()
        {
            log.Error(Failure);
            throw new Exception(Failure);
        }

        private void OverwriteOpenApiYaml(string containerApi)
        {
            Log("API compatibility check failed. Updating PrometheiPlugin...");
            var openApiFilePath = Path.Combine(PluginPathUtils.ProjectPluginsDir, "PrometheiClient", "openapi.yaml");
            if (!File.Exists(openApiFilePath)) throw new Exception("Unable to locate PrometheiClient/openapi.yaml. Expected: " + openApiFilePath);

            File.Delete(openApiFilePath);
            File.WriteAllText(openApiFilePath, containerApi);
            Log("PrometheiClient/openapi.yaml has been updated.");
        }

        private string Hash(string file)
        {
            var fileBytes = Encoding.ASCII.GetBytes(file
                .Replace(Environment.NewLine, ""));
            var sha = SHA256.Create();
            var hash = sha.ComputeHash(fileBytes);
            return BitConverter.ToString(hash);
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }
}
