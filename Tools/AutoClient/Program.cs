using ArgsUniform;
using AutoClient;
using AutoClient.Modes;
using PrometheiClient;
using GethPlugin;
using Utils;
using WebUtils;
using Logging;

public class Program
{
    private readonly App app;

    public Program(Configuration config)
    {
        app = new App(config);
    }

    public static void Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, args) => cts.Cancel();

        var uniformArgs = new ArgsUniform<Configuration>(PrintHelp, args);
        var config = uniformArgs.Parse(true);

        var p = new Program(config);
        p.Run();
    }

    public void Run()
    {
        Log("Setting up instances...");
        var prometheiNodes = CreatePrometheiWrappers();
        var nodeDispatcher = new NodeDispatcher(app.Log, prometheiNodes);

        var folderStore = new FolderStoreMode(app, nodeDispatcher);
        Log("Starting folder-store mode...");
        folderStore.Start();

        app.Cts.Token.WaitHandle.WaitOne();

        folderStore.Stop();

        Log("Done");
    }

    private PrometheiWrapper[] CreatePrometheiWrappers()
    {
        var endpointStrs = app.Config.PrometheiEndpoints
            .Replace(Environment.NewLine, ";")
            .Split(";", StringSplitOptions.RemoveEmptyEntries);
        var result = new List<PrometheiWrapper>();

        Log($"Checking {endpointStrs.Length} endpoints...");
        var i = 1;
        foreach (var e in endpointStrs)
        {
            result.Add(CreatePrometheiWrapper(e.Trim(), i));
            i++;
        }

        return result.ToArray();
    }

    private readonly string LogLevel = "TRACE;info:discv5,providers,routingtable,manager,cache;warn:libp2p,multistream,switch,transport,tcptransport,semaphore,asyncstreamwrapper,lpstream,mplex,mplexchannel,noise,bufferstream,mplexcoder,secure,chronosstream,connection,websock,ws-session,muxedupgrade,upgrade,identify,contracts,clock,serde,json,serialization,JSONRPC-WS-CLIENT,JSONRPC-HTTP-CLIENT,repostore";

    private PrometheiWrapper CreatePrometheiWrapper(string endpoint, int number)
    {
        var splitIndex = endpoint.LastIndexOf(':');
        var host = endpoint.Substring(0, splitIndex);
        var port = Convert.ToInt32(endpoint.Substring(splitIndex + 1));

        var address = new Address(
            logName: $"node@{host}:{port}",
            host: host,
            port: port
        );

        app.Log.Log($"'{address}': Creating wrapper...");

        var numberStr = number.ToString().PadLeft(3, '0');
        var modifyingPrefixer = new LogPrefixer(app.Log, "");
        var log = new LogPrefixer(modifyingPrefixer, $"[{numberStr}]");
        var httpFactory = new HttpFactory(log, new AutoClientWebTimeSet());
        var prometheiNodeFactory = new PrometheiNodeFactory(log: log, httpFactory: httpFactory, dataDir: app.Config.DataPath);
        var instance = PrometheiInstance.CreateFromApiEndpoint($"[AC-{numberStr}]", address, EthAccountGenerator.GenerateNew());
        var node = prometheiNodeFactory.CreatePrometheiNode(instance);

        node.SetLogLevel(LogLevel);

        app.Log.Log($"'{address}': Connect successful");
        return new PrometheiWrapper(app, node, modifyingPrefixer);
    }

    private void Log(string msg)
    {
        app.Log.Log(msg);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Uploads files and creates Promethei storage contracts for them.");
    }
}
