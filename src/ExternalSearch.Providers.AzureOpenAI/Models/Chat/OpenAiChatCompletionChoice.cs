using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models.Chat;

internal class OpenAiChatCompletionChoice
{
    [JsonProperty("message")]
    public OpenAiChatMessage? Message { get; set; }
}
