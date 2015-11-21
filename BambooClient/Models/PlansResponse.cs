using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class PlansResponse
    {
        [JsonProperty]
        public Hyperlink Link { get; private set; }
        [JsonProperty]
        public PlansCollection Plans { get; private set; }
    }
}
