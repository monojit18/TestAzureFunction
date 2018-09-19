using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace TestAzureFunction
{
    public class TAFProcessBlob
    {

        private static async Task<string> ParseOCRAsync(Byte[] blobContents)
        {

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "23699e57be9e49529ba4b9aa2ab9e396");

            var content = new ByteArrayContent(blobContents);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var res = await client.PostAsync("https://eastus.api.cognitive.microsoft.com/vision/v1.0/ocr", content);
            var parsedResults = await res.Content.ReadAsStringAsync();
            return parsedResults;

        } 

        [FunctionName("ProcessBlob")]
        public static async Task ProcessBlob([BlobTrigger("ocrinfoblob/{name}")]
                                                CloudBlockBlob cloudBlockBlob,
                                                [Blob("ocrinfoblob/{name}", FileAccess.ReadWrite)]
                                                Byte[] blobContents,
                                                [Queue("ocrinfoqueue")]
                                                IAsyncCollector<CloudQueueMessage> cloudQueueMessageCollector,
                                                TraceWriter log)
        {


            log.Info(cloudBlockBlob.Name);

            var parsedResultsString = await ParseOCRAsync(blobContents);
            var cloudQueueMessage = new CloudQueueMessage(parsedResultsString);
            await cloudQueueMessageCollector.AddAsync(cloudQueueMessage);
            
        }


    }
}