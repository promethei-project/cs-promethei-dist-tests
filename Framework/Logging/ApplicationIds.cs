namespace Logging
{
    public class ApplicationIds
    {
        public ApplicationIds(string prometheiId, string gethId, string prometheusId, string prometheiContractsId, string grafanaId)
        {
            PrometheiId = prometheiId;
            GethId = gethId;
            PrometheusId = prometheusId;
            PrometheiContractsId = prometheiContractsId;
            GrafanaId = grafanaId;
        }

        public string PrometheiId { get; }
        public string GethId { get; }
        public string PrometheusId { get; }
        public string PrometheiContractsId { get; }
        public string GrafanaId { get; }
    }
}
