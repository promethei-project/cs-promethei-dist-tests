using Utils;

namespace ArchivistPlugin
{
    public class ArchivistDockerImage
    {
        private const string DefaultDockerImage =
            //"durabilitylabs/archivist-node:sha-314a2c7-dist-tests";
            "durabilitylabs/archivist-node:sha-3a8e05d-dist-tests";

        public string GetArchivistDockerImage()
        {
            return EnvVar.GetOrDefault("ARCHIVISTDOCKERIMAGE", DefaultDockerImage);
        }
    }
}
