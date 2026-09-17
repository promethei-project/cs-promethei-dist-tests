using PrometheiContractsPlugin.Marketplace;
using Nethereum.Contracts;
using NethereumWorkflow;
using Newtonsoft.Json;
using System.Reflection;

namespace PrometheiContractsPlugin.ChainMonitor
{
    public class CallReporter
    {
        private readonly List<FunctionCallReport> reports;
        private readonly IPrometheiContractsEvents events;

        public CallReporter(List<FunctionCallReport> reports, IPrometheiContractsEvents events)
        {
            this.reports = reports;
            this.events = events;
        }

        public void Run(Action<MarkProofAsMissingFunction> onMarkedAsMissing)
        {
            // We get all the marketplace function types from the assembly,
            // sorted into view and transaction type buckets.
            var marketplaceTypes = PrometheiContractTypes.GetMarketplaceFunctionTypes();

            // We wrap them each in collectors.
            var views = marketplaceTypes.ViewFunctions.Select(t => new FunctionCollectorWrapper(t)).ToArray();
            var transactions = marketplaceTypes.TransactionFunctions.Select(t => new FunctionCollectorWrapper(t)).ToArray();

            // We load the function calls into the collectors.
            events.GetFunctionCalls(
                views.Select(v => v.Collector).Concat(
                    transactions.Select(t => t.Collector)
                ).ToArray()
            );

            // We create reports for each transaction call we see.
            foreach (var t in transactions)
            {
                CreateFunctionCallReport(t.GetCalls(), onMarkedAsMissing);
            }

            // We throw for each view function we see.
            // They should not end up in transactions on the blockchain.
            foreach (var t in views)
            {
                ThrowForCall(t.GetCalls());
            }
        }

        public class FunctionCollectorWrapper
        {
            private readonly Type type;

            public FunctionCollectorWrapper(Type type)
            {
                this.type = type;

                Collector = CreateCollector();
            }

            public IFunctionCallCollector Collector { get; }

            public IFunctionCall[] GetCalls()
            {
                return Collector.GetCalls();
            }

            private IFunctionCallCollector CreateCollector()
            {
                var genericMethod = GetType().GetMethod(nameof(CreateTypedCollector), BindingFlags.Static | BindingFlags.NonPublic);
                var typedMethod = genericMethod!.MakeGenericMethod(type);
                var result = typedMethod.Invoke(this, []);
                return (IFunctionCallCollector)result!;
            }

            private static IFunctionCallCollector CreateTypedCollector<TFunc>() where TFunc : FunctionMessage, new()
            {
                return new FunctionCallCollector<TFunc>();
            }
        }

        private void CreateFunctionCallReport(IFunctionCall[] calls, Action<MarkProofAsMissingFunction> onCall)
        {
            foreach (var call in calls)
            {
                var block = call.Block;
                var callData = call.GetCall();
                reports.Add(new FunctionCallReport(block, callData.GetType().Name, JsonConvert.SerializeObject(callData)));

                if (call.GetCall() is MarkProofAsMissingFunction markProofAsMissingFunction)
                {
                    onCall(markProofAsMissingFunction);
                }
            }
        }

        private void ThrowForCall(IFunctionCall[] calls)
        {
            if (calls.Length == 0) return;
            var name = calls[0].GetCall().GetType().Name;
            var msg = $"Detected transactions with calls to view function '{name}'. {string.Join(",", calls.Select(c => c.Block.ToString()).ToArray())}";
            throw new Exception(msg);
        }
    }
}
