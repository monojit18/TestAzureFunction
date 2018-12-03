using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TestAzureFunction.DataModels
{
    public class OCRDataModel
    {

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("textAngle")]
        public double TextAngle { get; set; }


    }

}
