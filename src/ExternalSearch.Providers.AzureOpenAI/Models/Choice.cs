using Newtonsoft.Json;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Models;

internal class Choice
{
    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("usage")]
    public string? Usage { get; set; }

    [JsonProperty("logprobs")]
    public int? LogProbs { get; set; }
}
