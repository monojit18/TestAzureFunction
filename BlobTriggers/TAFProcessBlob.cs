using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;
using Newtonsoft.Json;

namespace TestAzureFunction.BlobTriggers
{
    public class TAFProcessBlob
    {

        private static async Task<string> ParseOCRAsync(Byte[] blobContents, ILogger log)
        {

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "b2b86396662049ea949fb4f072353717");

            var content = new ByteArrayContent(blobContents);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var resliliency = DefineAndRetrieveResiliencyStrategy(log);

            // var ocrResponse = await resliliency.ExecuteAsync((() => client.PostAsync("https://eastus.api.cognitive.microsoft.com/vision/v1.0/ocr", content)));
            var ocrResponse = await client.PostAsync("https://eastus.api.cognitive.microsoft.com/vision/v1.0/ocr", content);
            var parsedResults = await ocrResponse.Content.ReadAsStringAsync();
            return parsedResults;

        } 

        private static PolicyWrap<HttpResponseMessage> DefineAndRetrieveResiliencyStrategy(ILogger log)
        {
            // Retry when these status codes are encountered.
            HttpStatusCode[] httpStatusCodesWorthRetrying = {
               HttpStatusCode.InternalServerError, // 500
               HttpStatusCode.BadGateway, // 502
               HttpStatusCode.GatewayTimeout // 504
            };

            // Immediately fail (fail fast) when these status codes are encountered.
            HttpStatusCode[] httpStatusCodesToImmediatelyFail = {
               HttpStatusCode.BadRequest, // 400
               HttpStatusCode.Unauthorized, // 401
               HttpStatusCode.Forbidden // 403
            };

            // Define our waitAndRetry policy: retry n times with an exponential backoff in case the Computer Vision API throttles us for too many requests.
            var waitAndRetryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(e => e.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    e.StatusCode == (System.Net.HttpStatusCode)429 || e.StatusCode == (System.Net.HttpStatusCode)403)
                .WaitAndRetryAsync(10, // Retry 10 times with a delay between retries before ultimately giving up
                    attempt => TimeSpan.FromSeconds(0.25 * Math.Pow(2, attempt)), // Back off!  2, 4, 8, 16 etc times 1/4-second
                                                                                  //attempt => TimeSpan.FromSeconds(6), // Wait 6 seconds between retries
                    (exception, calculatedWaitDuration) =>
                    {
                        log.LogDebug($"Computer Vision API server is throttling our requests. Automatically delaying for {calculatedWaitDuration.TotalMilliseconds}ms");
                    }
                );

            // Define our first CircuitBreaker policy: Break if the action fails 4 times in a row.
            // This is designed to handle Exceptions from the Computer Vision API, as well as
            // a number of recoverable status messages, such as 500, 502, and 504.
            var circuitBreakerPolicyForRecoverable = Policy
                .Handle<HttpResponseException>()
                .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(3),
                    onBreak: (outcome, breakDelay) =>
                    {
                        log.LogDebug($"Polly Circuit Breaker logging: Breaking the circuit for {breakDelay.TotalMilliseconds}ms due to: {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                    },
                    onReset: () => log.LogDebug("Polly Circuit Breaker logging: Call ok... closed the circuit again"),
                    onHalfOpen: () => log.LogDebug("Polly Circuit Breaker logging: Half-open: Next call is a trial")
                );

            // Combine the waitAndRetryPolicy and circuit breaker policy into a PolicyWrap. This defines our resiliency strategy.
            return Policy.WrapAsync(waitAndRetryPolicy, circuitBreakerPolicyForRecoverable);
        }

        [FunctionName("ProcessBlob")]
        public static async Task ProcessBlob([BlobTrigger("ocrinfoblob/{name}")]
                                                CloudBlockBlob cloudBlockBlob,
                                                [Blob("ocrinfoblob/{name}",
                                                FileAccess.ReadWrite)]
                                                Byte[] blobContents,
                                                [Queue("ocrinfoqueue")]
                                                IAsyncCollector<CloudQueueMessage>
                                                cloudQueueMessageCollector,
                                                ILogger log)
        {


            log.LogDebug(cloudBlockBlob.Name);

            var parsedResultsString = await ParseOCRAsync(blobContents, log);
            var cloudQueueMessage = new CloudQueueMessage(parsedResultsString);
            await cloudQueueMessageCollector.AddAsync(cloudQueueMessage);
            
        }


    }
}