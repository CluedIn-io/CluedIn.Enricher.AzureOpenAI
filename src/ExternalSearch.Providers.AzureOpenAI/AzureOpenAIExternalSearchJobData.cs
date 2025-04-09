using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI;

public class AzureOpenAIExternalSearchJobData : CrawlJobData
{
    public AzureOpenAIExternalSearchJobData(IDictionary<string, object> configuration)
    {
        ApiToken = GetValue<string>(configuration, Constants.KeyName.ApiToken);
        AcceptedEntityType = GetValue<string>(configuration, Constants.KeyName.AcceptedEntityType);
        BaseUrl = GetValue<string>(configuration, Constants.KeyName.BaseUrl);
        AiModel = GetValue<string>(configuration, Constants.KeyName.AiModel);
        Prompt = GetValue<string>(configuration, Constants.KeyName.Prompt);
        //ResponseVocabularyKey = GetValue<string>(configuration, Constants.KeyName.ResponseVocabularyKey);
    }

    public string ApiToken { get; set; }
    public string AcceptedEntityType { get; set; }
    public string BaseUrl { get; set; }
    public string AiModel { get; set; }
    public string Prompt { get; set; }
    //public string ResponseVocabularyKey { get; set; }

    public IDictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { Constants.KeyName.ApiToken, ApiToken },
            { Constants.KeyName.AcceptedEntityType, AcceptedEntityType },
            { Constants.KeyName.BaseUrl, BaseUrl },
            { Constants.KeyName.AiModel, AiModel },
            { Constants.KeyName.Prompt, Prompt },
            //{ Constants.KeyName.ResponseVocabularyKey, ResponseVocabularyKey },
        };
    }
}
