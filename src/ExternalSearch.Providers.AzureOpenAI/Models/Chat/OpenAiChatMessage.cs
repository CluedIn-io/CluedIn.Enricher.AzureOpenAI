using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models.Chat;

internal class OpenAiChatMessage
{
    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }
}
