using System;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class Hyperlink
    {
        [JsonProperty]
        public String HRef { get; set; }
        [JsonProperty]
        public String Rel { get; set; }
    }
}
