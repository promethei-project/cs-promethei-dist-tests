using System.Security.Cryptography;
using System.Text;
using Utils;

public static class Program
{
    private const string Search = "<INSERT-OPENAPI-YAML-HASH>";
    private const string PrometheiPluginFolderName = "PrometheiPlugin";
    private const string ProjectPluginsFolderName = "ProjectPlugins";

    public static void Main(string[] args)
    {
        Console.WriteLine("Injecting hash of 'openapi.yaml'...");

        var pluginRoot = FindPrometheiPluginFolder();
        var clientRoot = FindPrometheiClientFolder();
        Console.WriteLine("Located PrometheiPlugin: " + pluginRoot);
        Console.WriteLine("Located PrometheiClient: " + clientRoot);
        var openApiFile = Path.Combine(clientRoot, "openapi.yaml");
        var clientFile = Path.Combine(clientRoot, "obj", "openapiClient.cs");
        var targetFile = Path.Combine(pluginRoot, "ApiChecker.cs");

        var hash = CreateHash(openApiFile);
        // This hash is used to verify that the Promethei docker image being used is compatible
        // with the openapi.yaml being used by the Promethei plugin.
        // If the openapi.yaml files don't match, an exception is thrown.

        if (IsCurrentHash(hash, targetFile))
        {
            Console.WriteLine("Hash of openapi.yaml is unchanged.");
            return;
        }

        // Force client rebuild by deleting previous artifact.
        File.Delete(clientFile);

        SearchAndInject(hash, targetFile);

        // This program runs as the pre-build trigger for "PrometheiPlugin".
        // You might be wondering why this work isn't done by a shell script.
        // This is because this project is being run on many different platforms.
        // (Mac, Unix, Win, but also desktop/cloud containers.)
        // In order to not go insane trying to make a shell script that works in all possible cases,
        // instead we use the one tool that's definitely installed in all platforms and locations
        // when you're trying to run this plugin.

        Console.WriteLine("Done!");
    }

    private static string FindPrometheiPluginFolder()
    {
        var folder = Path.Combine(PluginPathUtils.ProjectPluginsDir, "PrometheiPlugin");
        if (!Directory.Exists(folder)) throw new Exception("PrometheiPlugin folder not found. Expected: " + folder);
        return folder;
    }

    private static string FindPrometheiClientFolder()
    {
        var folder = Path.Combine(PluginPathUtils.ProjectPluginsDir, "PrometheiClient");
        if (!Directory.Exists(folder)) throw new Exception("PrometheiClient folder not found. Expected: " + folder);
        return folder;
    }

    private static string CreateHash(string openApiFile)
    {
        var file = File.ReadAllText(openApiFile);
        var fileBytes = Encoding.ASCII.GetBytes(file
            .Replace(Environment.NewLine, ""));

        var sha = SHA256.Create();
        var hash = sha.ComputeHash(fileBytes);
        return BitConverter.ToString(hash);
    }

    private static void SearchAndInject(string hash, string targetFile)
    {
        var lines = File.ReadAllLines(targetFile);
        Inject(lines, hash);
        File.WriteAllLines(targetFile, lines);
    }

    private static bool IsCurrentHash(string expectedHash, string targetFile)
    {
        return File.ReadAllText(targetFile).Contains(expectedHash);
    }

    private static void Inject(string[] lines, string hash)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(Search))
            {
                lines[i + 1] = $"        private const string OpenApiYamlHash = \"{hash}\";";
                return;
            }
        }
    }
}
