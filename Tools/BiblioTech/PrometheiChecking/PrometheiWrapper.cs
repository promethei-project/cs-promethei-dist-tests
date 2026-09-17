using System.Net.Http.Headers;
using System.Text;
using PrometheiClient;
using Logging;
using Utils;
using WebUtils;

namespace BiblioTech.PrometheiChecking
{
    public class PrometheiWrapper
    {
        private readonly PrometheiNodeFactory factory;
        private readonly ILog log;
        private readonly Configuration config;
        private readonly object prometheiLock = new object();
        private IPrometheiNode? currentPrometheiNode;

        public PrometheiWrapper(ILog log, Configuration config)
        {
            this.log = log;
            this.config = config;

            var httpFactory = CreateHttpFactory();
            factory = new PrometheiNodeFactory(log, httpFactory, dataDir: config.DataPath);

            Task.Run(CheckPrometheiNode);
        }

        public T? OnPromethei<T>(Func<IPrometheiNode, T> func) where T : class
        {
            lock (prometheiLock)
            {
                if (currentPrometheiNode == null) return null;
                return func(currentPrometheiNode);
            }
        }

        private void CheckPrometheiNode()
        {
            Thread.Sleep(TimeSpan.FromSeconds(10.0));

            while (true)
            {
                lock (prometheiLock)
                {
                    var newNode = GetNewPrometheiNode();
                    if (newNode != null && currentPrometheiNode == null) ShowConnectionRestored();
                    if (newNode == null && currentPrometheiNode != null) ShowConnectionLost();
                    currentPrometheiNode = newNode;
                }

                Thread.Sleep(TimeSpan.FromMinutes(15.0));
            }
        }

        private IPrometheiNode? GetNewPrometheiNode()
        {
            try
            {
                if (currentPrometheiNode != null)
                {
                    try
                    {
                        // Current instance is responsive? Keep it.
                        var info = currentPrometheiNode.GetDebugInfo();
                        if (info != null && info.Version != null &&
                            !string.IsNullOrEmpty(info.Version.Revision)) return currentPrometheiNode;
                    }
                    catch
                    {
                    }
                }

                return CreatePromethei();
            }
            catch (Exception ex)
            {
                log.Error("Exception when trying to check promethei node: " + ex.Message);
                return null;
            }
        }

        private void ShowConnectionLost()
        {
            Program.AdminChecker.SendInAdminChannel("Promethei node connection lost.").Wait();
        }

        private void ShowConnectionRestored()
        {
            Program.AdminChecker.SendInAdminChannel("Promethei node connection restored.").Wait();
        }

        private IPrometheiNode CreatePromethei()
        {
            var endpoint = config.PrometheiEndpoint;
            var splitIndex = endpoint.LastIndexOf(':');
            var host = endpoint.Substring(0, splitIndex);
            var port = Convert.ToInt32(endpoint.Substring(splitIndex + 1));

            var address = new Address(
                logName: $"cdx@{host}:{port}",
                host: host,
                port: port
            );

            var instance = PrometheiInstance.CreateFromApiEndpoint("ac", address);
            return factory.CreatePrometheiNode(instance);
        }

        private HttpFactory CreateHttpFactory()
        {
            if (string.IsNullOrEmpty(config.PrometheiEndpointAuth) || !config.PrometheiEndpointAuth.Contains(":"))
            {
                return new HttpFactory(log, new SnappyTimeSet());
            }

            var tokens = config.PrometheiEndpointAuth.Split(':');
            if (tokens.Length != 2) throw new Exception("Expected '<username>:<password>' in PrometheiEndpointAuth parameter.");

            return new HttpFactory(log, new SnappyTimeSet(), onClientCreated: client =>
            {
                client.DefaultRequestHeaders.Authorization = new BasicAuthenticationHeaderValue(tokens[0], tokens[1]);
            });
        }

        public class SnappyTimeSet : IWebCallTimeSet
        {
            public TimeSpan HttpCallRetryDelay()
            {
                return TimeSpan.FromSeconds(1.0);
            }

            public TimeSpan HttpCallTimeout()
            {
                return TimeSpan.FromSeconds(3.0);
            }

            public TimeSpan HttpRetryTimeout()
            {
                return TimeSpan.FromSeconds(12.0);
            }
        }

        /// <summary>
        /// From https://github.com/DuendeArchive/IdentityModel/blob/main/src/Client/BasicAuthenticationHeaderValue.cs#L13
        /// </summary>
        public class BasicAuthenticationHeaderValue : AuthenticationHeaderValue
        {
            public BasicAuthenticationHeaderValue(string userName, string password)
                : base("Basic", EncodeCredential(userName, password))
            { }
            
            public static string EncodeCredential(string userName, string password)
            {
                if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentNullException(nameof(userName));
                if (password == null) password = "";

                Encoding encoding = Encoding.UTF8;
                string credential = string.Format("{0}:{1}", userName, password);

                return Convert.ToBase64String(encoding.GetBytes(credential));
            }
        }
    }
}
