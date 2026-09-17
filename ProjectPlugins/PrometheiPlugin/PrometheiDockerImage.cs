using Utils;

namespace PrometheiPlugin
{
    public class PrometheiDockerImage
    {
        private const string DefaultDockerImage =
            //"durabilitylabs/nim-promethei-node:sha-314a2c7-dist-tests";
            "durabilitylabs/nim-promethei-node:sha-d5fad42-dist-tests";

        public string GetPrometheiDockerImage()
        {
            return EnvVar.GetOrDefault("PROMETHEIDOCKERIMAGE", DefaultDockerImage);
        }
    }
}
