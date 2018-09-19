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
using Newtonsoft.Json;

namespace TestAzureFunction
{
    public class TAFProcessQueue
    {

        [FunctionName("ProcessQueue")]
        [return: Table("ocrinfotable")]
        public static OCRInfoRow ProcessQueue([QueueTrigger("ocrinfoqueue")]
                                                CloudQueueMessage cloudQueueMessage,
                                                TraceWriter log)
        {

            var queueMessageString = cloudQueueMessage.AsString;
            log.Info(queueMessageString);

            var partitionKeyString = "level";
            var rowkeyString = string.Concat(partitionKeyString, "-", cloudQueueMessage.Id);
            var contactInfoString = string.Empty;

            if (string.IsNullOrEmpty(queueMessageString) == false)
                contactInfoString = string.Copy(queueMessageString);

            var ocrInfoRow = new OCRInfoRow()
            {

                PartitionKey = partitionKeyString,
                RowKey = rowkeyString,
                ContactInfo = contactInfoString

            };

            return ocrInfoRow;
            
        }


    }

    public class OCRInfoRow : TableEntity
    {

        public string ContactInfo { get; set; }

    }
}


