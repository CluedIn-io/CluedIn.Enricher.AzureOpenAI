using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models
{
    internal class OpenAiErrorResponse
    {
        [JsonProperty("error")]
        public OpenAiErrorDetails? Error { get; set; }
    }
}
