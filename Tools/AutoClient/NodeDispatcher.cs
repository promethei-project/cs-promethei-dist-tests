using Logging;

namespace AutoClient
{
    public class NodeDispatcher
    {
        private readonly ILog log;
        private readonly List<PrometheiWrapper> nodes;
        private readonly object nodesLock = new object();

        public NodeDispatcher(ILog log, PrometheiWrapper[] nodes)
        {
            this.log = new LogPrefixer(log, "(Dispatch)");
            this.nodes = nodes.ToList();
        }

        public void OnNode(string prefix, Action<PrometheiWrapper> action, Action whenDone)
        {
            var node = TakeNode();

            Task.Run(() =>
            {
                try
                {
                    node.SetLogPrefix(prefix);
                    action(node);
                }
                catch (Exception ex)
                {
                    log.Error($"'{prefix}': {ex}");
                }
                finally
                {
                    node.SetLogPrefix(string.Empty);
                    whenDone();
                }

                ReleaseNode(node);
            });
        }

        private PrometheiWrapper TakeNode()
        {
            var wait = false;
            while (true)
            {
                while (nodes.Count == 0)
                {
                    if (!wait)
                    {
                        wait = true;
                        log.Log("Waiting for Promethei node to become available...");
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                lock (nodesLock)
                {
                    if (nodes.Count > 0)
                    {
                        var node = nodes.First();
                        nodes.RemoveAt(0);
                        return node;
                    }
                }
            }
        }

        private void ReleaseNode(PrometheiWrapper node)
        {
            lock (nodesLock)
            {
                nodes.Add(node);
            }
        }
    }
}
