namespace PrometheiPlugin
{
    public class PrometheiExePath
    {
        private readonly string[] paths = [
            Path.Combine("d:", "Dev", "nim-promethei-node", "build", "promethei.exe"),
            Path.Combine("c:", "Projects", "nim-promethei-node", "build", "promethei.exe")
        ];

        private string selectedPath = string.Empty;

        public PrometheiExePath()
        {
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    selectedPath = p;
                    return;
                }
            }
        }

        public string Get()
        {
            return selectedPath;
        }
    }
}
