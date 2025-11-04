using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI;

public class AzureOpenAIExternalSearchJobData : CrawlJobData
{
    public AzureOpenAIExternalSearchJobData(IDictionary<string, object> configuration)
    {
        AcceptedEntityType = GetValue<string>(configuration, Constants.KeyName.AcceptedEntityType);
        BaseUrl = GetValue<string>(configuration, Constants.KeyName.BaseUrl);
        ApiKey = GetValue<string>(configuration, Constants.KeyName.ApiKey);
        AiDeployment = GetValue<string>(configuration, Constants.KeyName.AiDeployment);
        Prompt = GetValue<string>(configuration, Constants.KeyName.Prompt);
    }

    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public string AcceptedEntityType { get; set; }
    public string AiDeployment { get; set; }
    public string Prompt { get; set; }

    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { Constants.KeyName.AcceptedEntityType, AcceptedEntityType },
            { Constants.KeyName.BaseUrl, BaseUrl },
            { Constants.KeyName.ApiKey, ApiKey },
            { Constants.KeyName.AiDeployment, AiDeployment },
            { Constants.KeyName.Prompt, Prompt },
        };
    }
}
