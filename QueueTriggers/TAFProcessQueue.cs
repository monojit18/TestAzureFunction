using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TestAzureFunction.DataModels;

namespace TestAzureFunction.QueueTriggers
{
    public class TAFProcessQueue
    {
        private static async Task<bool> SaveBlobNameAsync(string ocrInfoString)
        {

            var endPointString = Environment.GetEnvironmentVariable("CosmosDBEndPoint");
            var authKeyString = Environment.GetEnvironmentVariable("CosmosDBAuthKeyPoint"); 
            var databaseIdString = Environment.GetEnvironmentVariable("CosmosDBDatabaseId");
            var collectionIdString = Environment.GetEnvironmentVariable("CosmosDBCollectionId");

            var documentClient = new DocumentClient(new Uri(endPointString),
                                                    authKeyString);

            var documentURI = UriFactory.CreateDocumentCollectionUri(databaseIdString,
                                                                        collectionIdString);
            if (documentURI == null)
                return false;

            var documentDataModel = new DocumentDataModel()
            {

                DocumentInfoString = ocrInfoString
            };

            var createResponse = await documentClient.CreateDocumentAsync(documentURI,
                                                                            documentDataModel);
            var couldCreate = (createResponse.StatusCode == HttpStatusCode.Created
                                || createResponse.StatusCode == HttpStatusCode.OK);
            
            return couldCreate;

        }

        [FunctionName("ProcessQueue")]
        public static async Task ProcessQueue([QueueTrigger("ocrinfoqueue")]
                                                CloudQueueMessage cloudQueueMessage,
                                                ILogger log)
        {

            var queueMessageString = cloudQueueMessage.AsString;
            log.LogDebug(queueMessageString);

            await SaveBlobNameAsync(queueMessageString);
            
        }


        // [FunctionName("ProcessQueue")]
        // [return: Table("ocrinfotable")]
        // public static OCRInfoRow ProcessQueue([QueueTrigger("ocrinfoqueue")]
        //                                         CloudQueueMessage cloudQueueMessage,
        //                                         ILogger log)
        // {

        //     var queueMessageString = cloudQueueMessage.AsString;
        //     log.LogDebug(queueMessageString);

        //     var partitionKeyString = "level";
        //     var rowkeyString = string.Concat(partitionKeyString, "-", cloudQueueMessage.Id);
        //     var contactInfoString = string.Empty;

        //     if (string.IsNullOrEmpty(queueMessageString) == false)
        //         contactInfoString = string.Copy(queueMessageString);

        //     var ocrInfoRow = new OCRInfoRow()
        //     {

        //         PartitionKey = partitionKeyString,
        //         RowKey = rowkeyString,
        //         ContactInfo = contactInfoString

        //     };

        //     return ocrInfoRow;
            
        // }


    }

    public class OCRInfoRow : TableEntity
    {

        public string ContactInfo { get; set; }

    }
}


