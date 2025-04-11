using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models.Chat
{
    internal class OpenAiChatCompletionRequest
    {
        [JsonProperty("messages")]
        public OpenAiChatMessage[]? Messages { get; set; }

        [JsonProperty("temperature")]
        public int Temperature { get; set; }

        [JsonProperty("stream", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool Stream { get; set; }
    }
}
