using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models.Chat;

internal class OpenAiChatCompletionResponse
{
    [JsonProperty("choices")]
    public OpenAiChatCompletionChoice[]? Choices { get; set; }
}
