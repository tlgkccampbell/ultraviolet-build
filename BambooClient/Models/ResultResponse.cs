using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class ResultsResponse
    {
        [JsonProperty]
        public Hyperlink Link { get; private set; }
        [JsonProperty]
        public ResultCollection Results { get; private set; }
    }
}
