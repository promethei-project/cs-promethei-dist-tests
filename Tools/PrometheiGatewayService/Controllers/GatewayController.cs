using Logging;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace PrometheiGatewayService.Controllers;

[ApiController]
public class GatewayController : ControllerBase
{
    private readonly HttpClient client = new HttpClient();
    private readonly NodeSelector selector;
    private readonly AppMetrics metrics;
    private readonly ILog log;

    public GatewayController(NodeSelector selector, Configuration config, AppMetrics metrics, ILog log)
    {
        this.selector = selector;
        this.metrics = metrics;
        this.log = log;
        client.Timeout = TimeSpan.FromMinutes(config.RequestTimeoutMinutes);
    }

    [HttpGet()]
    [Route("manifest/{cid}")]
    public async Task GetManifest(string cid)
    {
        var nodeUrl = selector.GetNodeUrl(cid);
        var sourceUrl = $"{nodeUrl}data/{cid}/network/manifest";
        await StreamResponse(sourceUrl, MediaTypeWithQualityHeaderValue.Parse("application/json"));
        metrics.OnManifestRequest();
    }

    [HttpGet()]
    [Route("data/{cid}")]
    public async Task GetData(string cid)
    {
        var nodeUrl = selector.GetNodeUrl(cid);
        var sourceUrl = $"{nodeUrl}data/{cid}/network/stream";

        await StreamResponse(sourceUrl, MediaTypeWithQualityHeaderValue.Parse("application/octet-stream"));
        metrics.OnDataRequest();
    }

    private async Task StreamResponse(string sourceUrl, MediaTypeWithQualityHeaderValue acceptHeader)
    {
        log.Log($"GET: '{sourceUrl}'");

        using var request = new HttpRequestMessage();
        request.Method = new HttpMethod("GET");
        request.Headers.Accept.Add(acceptHeader);
        request.RequestUri = new Uri(sourceUrl, UriKind.Absolute);

        var clientResponse = await client.SendAsync(request);
        var status = clientResponse.StatusCode;

        var responseStream = await clientResponse.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(Response.Body);
    }
}
