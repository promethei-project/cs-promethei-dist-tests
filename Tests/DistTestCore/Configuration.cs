using Core;
using KubernetesWorkflow;
using Utils;

namespace DistTestCore
{
    public class Configuration
    {
        private readonly string? kubeConfigFile;
        private readonly string logPath;
        private readonly string dataFilesPath;

        public Configuration()
        {
            kubeConfigFile = EnvVar.GetNullableOrDefault("KUBECONFIG");
            logPath = EnvVar.GetOrDefault("LOGPATH", "PrometheiTestLogs");
            dataFilesPath = EnvVar.GetOrDefault("DATAFILEPATH", "TestDataFiles");
        }

        public Configuration(string? kubeConfigFile, string logPath, string dataFilesPath)
        {
            this.kubeConfigFile = kubeConfigFile;
            this.logPath = logPath;
            this.dataFilesPath = dataFilesPath;
        }

        public KubernetesWorkflow.Configuration GetK8sConfiguration(IK8sTimeSet timeSet, string k8sNamespace)
        {
            return GetK8sConfiguration(timeSet, new DoNothingK8sHooks(), k8sNamespace);
        }

        public KubernetesWorkflow.Configuration GetK8sConfiguration(IK8sTimeSet timeSet, IK8sHooks hooks, string k8sNamespace)
        {
            var config = new KubernetesWorkflow.Configuration(
                kubeConfigFile: kubeConfigFile,
                operationTimeout: timeSet.K8sOperationTimeout(),
                retryDelay: timeSet.K8sOperationRetryDelay(),
                kubernetesNamespace: k8sNamespace
            );

            config.AllowNamespaceOverride = false;
            config.Hooks = hooks;
            config.ImagePullPolicy = EnvVar.GetOrDefault("IMAGE_PULL_POLICY", "Always");

            return config;
        }

        public Logging.LogConfig GetLogConfig()
        {
            return new Logging.LogConfig(logPath);
        }

        public string GetFileManagerFolder()
        {
            return dataFilesPath;
        }
    }
}
