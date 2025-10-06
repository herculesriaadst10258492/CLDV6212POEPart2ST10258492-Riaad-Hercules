using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABCRetailByRH.Functions;

public class Ping
{
    [Function("Ping")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.WriteString("OK");
        return res;
    }
}
