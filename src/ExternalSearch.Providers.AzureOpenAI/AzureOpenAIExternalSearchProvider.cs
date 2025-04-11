using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using CluedIn.Core;
using CluedIn.Core.Agent;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.ExternalSearch;
using CluedIn.Core.Processing;
using CluedIn.Core.Providers;
using CluedIn.ExternalSearch.Provider;
using CluedIn.ExternalSearch.Providers.AzureOpenAI.Models;
using CluedIn.Rules.Tokens;
using EntityType = CluedIn.Core.Data.EntityType;
using System.Web;
using CluedIn.ExternalSearch.Providers.AzureOpenAI.Models.Chat;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using ExecutionContext = CluedIn.Core.ExecutionContext;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI;

/// <summary>The Azure OpenAI external search provider.</summary>
/// <seealso cref="ExternalSearchProviderBase" />
// ReSharper disable once InconsistentNaming
public class AzureOpenAIExternalSearchProvider : ExternalSearchProviderBase, IExtendedEnricherMetadata,
    IConfigurableExternalSearchProvider, IExternalSearchProviderWithVerifyConnection
{
    private readonly IMemoryCache _cache;
    /**********************************************************************************************************
     * FIELDS
     **********************************************************************************************************/

    private static readonly EntityType[] _defaultAcceptedEntityTypes = [EntityType.Organization];

    /**********************************************************************************************************
     * CONSTRUCTORS
     **********************************************************************************************************/

    public AzureOpenAIExternalSearchProvider(IMemoryCache cache)
        : base(Constants.ProviderId, _defaultAcceptedEntityTypes)
    {
        _cache = cache;
        var nameBasedTokenProvider = new NameBasedTokenProvider("AzureOpenAI");

        if (nameBasedTokenProvider.ApiToken != null)
        {
            TokenProvider = new RoundRobinTokenProvider(
                nameBasedTokenProvider.ApiToken.Split(',', ';'));
        }
    }

    /**********************************************************************************************************
     * METHODS
     **********************************************************************************************************/

    public IEnumerable<EntityType> Accepts(IDictionary<string, object> config, IProvider provider)
    {
        return Accepts(config);
    }

    public IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request,
        IDictionary<string, object> config, IProvider provider)
    {
        return InternalBuildQueries(context, request, new AzureOpenAIExternalSearchJobData(config));
    }

    public IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query,
        IDictionary<string, object> config, IProvider provider)
    {
        var jobData = new AzureOpenAIExternalSearchJobData(config);
        return InternalExecuteSearch(context, query, jobData);
    }

    public IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query,
        IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config,
        IProvider provider)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using (context.Log.BeginScope("{0} {1}: query {2}, request {3}, result {4}", GetType().Name, "BuildClues", query, request, result))
        {
            //var deploymentName = query.QueryParameters["deploymentName"].Single();

            var resultItem = result.As<JObject>();
            var clue = new Clue(new EntityCode(request.EntityMetaData.EntityType, "AI", $"{query.QueryKey}{request.EntityMetaData.OriginEntityCode}".ToDeterministicGuid()), context.Organization);

            // add request.EntityMetaData.OriginEntityCode to the codes so that this clue will merge
            clue.Data.EntityData.Codes.Add(request.EntityMetaData.OriginEntityCode);

            //Temporary commented, it causes the Created By in UI to be the author if there is no original author
            //clue.Data.EntityData.Authors.Add(deploymentName); 

            PopulateMetadata(clue.Data.EntityData, resultItem, request);

            context.Log.LogInformation(
                "Clue produced, Id: '{Id}' OriginEntityCode: '{OriginEntityCode}' RawText: '{RawText}'", clue.Id,
                clue.OriginEntityCode, clue.RawText);

            return [clue];
        }
    }

    public IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result,
        IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name, "GetPrimaryEntityMetadata",
                   request, result))
        {
            var metadata = CreateMetadata(result.As<JObject>(), request);

            context.Log.LogInformation(
                "Primary entity meta data created, Name: '{Name}' OriginEntityCode: '{OriginEntityCode}'",
                metadata.Name, metadata.OriginEntityCode.Origin.Code);

            return metadata;
        }

    }

    public IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result,
        IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name,
                   "GetPrimaryEntityPreviewImage", request, result))
        {
            context.Log.LogInformation("Primary entity preview image not produced, returning null");

            return null;
        }
    }

    /**********************************************************************************************************
     * PROPERTIES
     **********************************************************************************************************/

    public string Icon { get; } = Constants.Icon;
    public string Domain { get; } = Constants.Domain;
    public string About { get; } = Constants.About;

    public AuthMethods AuthMethods { get; } = Constants.AuthMethods;
    public IEnumerable<Control> Properties { get; } = Constants.Properties;
    public Guide Guide { get; } = Constants.Guide;
    public IntegrationType Type { get; } = Constants.IntegrationType;

    private bool DeploymentSupportsCompletion(ExecutionContext executionContext, string deploymentName)
    {
        var baseUrl = executionContext.Organization.Settings.GetValue("OpenAiBaseUrl", "OpenAiBaseUrl", "");

        var deploymentSupportsCompletionCacheKey = $"{nameof(DeploymentSupportsCompletion)}_{executionContext.Organization.Id}_{baseUrl}_{deploymentName}";
        if (_cache.TryGetValue(deploymentSupportsCompletionCacheKey, out var cached) && cached != null)
        {
            return (bool)cached;
        }

        lock (this)
        {
            if (_cache.TryGetValue(deploymentSupportsCompletionCacheKey, out cached) && cached != null)
            {
                return (bool)cached;
            }

            var supportsCompletion = true;

            var apiKey = executionContext.Organization.Settings.GetValue("OpenAiApiKey", "OpenAiApiKey", "");

            baseUrl = baseUrl.TrimEnd('/');

            var client = new RestClient(baseUrl);
            var request =
                new RestRequest(
                    $"/openai/deployments/{HttpUtility.UrlEncode(deploymentName)}/completions?api-version=2022-12-01",
                    Method.POST);
            request.AddHeader("api-key", apiKey);
            request.AddParameter("application/json",
                JsonConvert.SerializeObject(new OpenAiCompletionRequest
                {
                    Prompt = "Hello",
                    BestOf = 1,
                    FrequencyPenalty = 0,
                    MaxTokens = 2000,
                    PresencePenalty = 0,
                    Stop = null,
                    Temperature = 0,
                    TopP = 0.5f
                }), ParameterType.RequestBody);
            var response = client.Execute<OpenAiResponse>(request);

            if (response.StatusCode == HttpStatusCode.BadRequest &&
                response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                var error = JsonConvert.DeserializeObject<OpenAiErrorResponse>(response.Content);

                if (error?.Error?.Code == "OperationNotSupported")
                {
                    supportsCompletion = false;

                    _cache.Set(deploymentSupportsCompletionCacheKey, supportsCompletion, DateTimeOffset.Now.AddMinutes(5));
                }
            }

            if (response.IsSuccessful)
            {
                _cache.Set(deploymentSupportsCompletionCacheKey, supportsCompletion, DateTimeOffset.Now.AddMinutes(5));
            }

            return supportsCompletion;
        }
    }


    public ConnectionVerificationResult VerifyConnection(ExecutionContext context,
        IReadOnlyDictionary<string, object> config)
    {
        var baseUrl = context.Organization.Settings.GetValue("OpenAiBaseUrl", "OpenAiBaseUrl", "");
        var apiKey = context.Organization.Settings.GetValue("OpenAiApiKey", "OpenAiApiKey", "");
        var deploymentName = config[Constants.KeyName.AiDeployment] as string;
        var prompt = config[Constants.KeyName.Prompt] as string;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new ConnectionVerificationResult(false, "OpenAI Base Url must not be blank");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ConnectionVerificationResult(false, "OpenAI API Key must not be blank");
        }

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return new ConnectionVerificationResult(false, "Deployment Name must not be blank");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new ConnectionVerificationResult(false, "Prompt must not be blank");
        }

        if (Regex.Matches(prompt, OutputMatchesPattern).Count == 0)
        {
            return new ConnectionVerificationResult(false, "Prompt must contain at least one output. eg, {output:vocabulary:product.description}");
        }

        var deploymentSupportsCompletion = DeploymentSupportsCompletion(context, deploymentName);

        prompt = "Hello";

        try
        {
            var response = deploymentSupportsCompletion
                ? QueryInternalUsingCompletionApi(context, deploymentName, prompt)
                : QueryInternalUsingChatApi(context, deploymentName, prompt);

            return new ConnectionVerificationResult(!string.IsNullOrWhiteSpace(response), "Empty response receive from AI");
        }
        catch (Exception ex)
        {
            var message = ex.Message;

            if (message.Contains("NotFound"))
            {
                message += $". Please verify that the deployment '{deploymentName}' exists";
            }

            return new ConnectionVerificationResult(false, message);
        }
    }

    private IEnumerable<EntityType> Accepts(IDictionary<string, object> config)
    {
        return Accepts(new AzureOpenAIExternalSearchJobData(config));
    }

    private IEnumerable<EntityType> Accepts(AzureOpenAIExternalSearchJobData config)
    {
        if (!string.IsNullOrWhiteSpace(config.AcceptedEntityType))
        {
            // If configured, only accept the configured entity types
            return [config.AcceptedEntityType];
        }

        // Fallback to default accepted entity types
        return _defaultAcceptedEntityTypes;
    }

    private bool Accepts(AzureOpenAIExternalSearchJobData config, EntityType entityTypeToEvaluate)
    {
        var configurableAcceptedEntityTypes = Accepts(config).ToArray();

        return configurableAcceptedEntityTypes.Any(entityTypeToEvaluate.Is);
    }

    private IEnumerable<IExternalSearchQuery> InternalBuildQueries(ExecutionContext context,
        IExternalSearchRequest request, AzureOpenAIExternalSearchJobData config)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using (context.Log.BeginScope($"{GetType().Name} BuildQueries: request {request}"))
        {
            if (!Accepts(config, request.EntityMetaData.EntityType))
            {
                context.Log.LogTrace("Unacceptable entity type from '{EntityName}', entity code '{EntityCode}'",
                    request.EntityMetaData.DisplayName, request.EntityMetaData.EntityType.Code);
                yield break;
            }

            context.Log.LogTrace("Starting to build queries for {EntityName}", request.EntityMetaData.DisplayName);

            var entityType = request.EntityMetaData.EntityType;

            var ruleTokenParser = context.ApplicationContext.Container.Resolve<IRuleTokenParser<IRuleActionToken>>();
            using var processingContext = context.ApplicationContext.CreateProcessingContext(context.Organization, JobRunId.Empty);
            var prompt = ruleTokenParser.Parse(processingContext, (IEntityMetadataPart)request.EntityMetaData, config.Prompt);

            var outputMatches = Regex.Matches(prompt, OutputMatchesPattern).OfType<Match>();

            prompt += $$"""
                       
                       Response in JSON using the following template
                       ###
                       {
                           {{string.Join("\n    ", outputMatches.Select(m => $"""
                                                                              "{m.Groups[1].Value}": ""
                                                                              """).Distinct())}}
                       }
                       ###
                       """;

            var queryDict = new Dictionary<string, string>
            {
                { "prompt", prompt },
                { "deploymentName", config.AiDeployment },
            };

            yield return new ExternalSearchQuery(this, entityType, queryDict);

            context.Log.LogTrace("Finished building queries for '{Name}'", request.EntityMetaData.Name);
        }
    }

    private const string OutputMatchesPattern = "{(output:[^}]+?)}";

    private IEnumerable<IExternalSearchQueryResult> InternalExecuteSearch(ExecutionContext context, IExternalSearchQuery query, AzureOpenAIExternalSearchJobData jobData)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        using (context.Log.BeginScope("{0} {1}: query {2}", GetType().Name, "ExecuteSearch", query))
        {
            context.Log.LogTrace("Starting external search for Id: '{Id}' QueryKey: '{QueryKey}'", query.Id, query.QueryKey);

            var prompt = query.QueryParameters["prompt"].Single();
            var deploymentName = query.QueryParameters["deploymentName"].Single();

            var deploymentSupportsCompletion = DeploymentSupportsCompletion(context, deploymentName);

            var response = deploymentSupportsCompletion ?
                QueryInternalUsingCompletionApi(context, deploymentName, prompt) :
                QueryInternalUsingChatApi(context, deploymentName, prompt);

            JObject jsonResponse;

            try
            {
                jsonResponse = JObject.Parse(response);
            }
            catch
            {
                prompt += "\n\nImportant: A prior attempt to answer this question resulted in malformed JSON. Please retry and verify that the output adheres strictly to the specified JSON format. The response must consist solely of valid JSON, as it will be programmatically processed.";

                response = deploymentSupportsCompletion ?
                    QueryInternalUsingCompletionApi(context, deploymentName, prompt) :
                    QueryInternalUsingChatApi(context, deploymentName, prompt);

                jsonResponse = JObject.Parse(response);
            }

            yield return new ExternalSearchQueryResult<JObject>(query, jsonResponse);
        }
    }

    private string QueryInternalUsingCompletionApi(ExecutionContext executionContext, string deploymentName, string prompt, bool logError = true)
    {
        var baseUrl = executionContext.Organization.Settings.GetValue("OpenAiBaseUrl", "OpenAiBaseUrl", "");
        var apiKey = executionContext.Organization.Settings.GetValue("OpenAiApiKey", "OpenAiApiKey", "");

        baseUrl = baseUrl.TrimEnd('/');

        var client = new RestClient(baseUrl);
        var request = new RestRequest($"/openai/deployments/{HttpUtility.UrlEncode(deploymentName)}/completions?api-version=2022-12-01", Method.POST);
        request.AddHeader("api-key", apiKey);
        request.AddParameter("application/json", JsonConvert.SerializeObject(new OpenAiCompletionRequest
        {
            Prompt = prompt,
            BestOf = 1,
            FrequencyPenalty = 0,
            MaxTokens = 2000,
            PresencePenalty = 0,
            Stop = null,
            Temperature = 0,
            TopP = 0.5f
        }), ParameterType.RequestBody);
        var response = client.Execute<OpenAiResponse>(request);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Thread.Sleep(2000); // while developing we observed that the error message says to retry in 2s
            throw new Exception($"Too many requests - Call to openai returned HTTP {response.StatusCode}"); // hack the message must start with 'Too many requests' for the core to retry
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            executionContext.Log.LogError(response.Content);
            throw new Exception($"Call to openai returned HTTP {response.StatusCode}");
        }

        if (response.Data == null)
        {
            throw new Exception(
                $"openai returned HTTP {response.StatusCode} but {nameof(response)}.{nameof(response.Data)} was null");
        }

        var first = response.Data.Choices.SafeEnumerate().FirstOrDefault();

        if (first?.Text == null)
        {
            throw new Exception("openai returned null");
        }

        return first.Text.TrimStart('\n');
    }

    private string QueryInternalUsingChatApi(ExecutionContext executionContext, string deploymentName, string prompt)
    {
        var baseUrl = executionContext.Organization.Settings.GetValue("OpenAiBaseUrl", "OpenAiBaseUrl", "");
        var apiKey = executionContext.Organization.Settings.GetValue("OpenAiApiKey", "OpenAiApiKey", "");

        baseUrl = baseUrl.TrimEnd('/');

        var client = new RestClient(baseUrl);

        var request = new RestRequest($"/openai/deployments/{HttpUtility.UrlEncode(deploymentName)}/chat/completions?api-version=2024-06-01", Method.POST);
        request.AddHeader("api-key", apiKey);
        request.AddParameter("application/json", JsonConvert.SerializeObject(new OpenAiChatCompletionRequest
        {
            Messages =
            [
                new OpenAiChatMessage { Role = "user", Content = prompt }
            ],
            Temperature = 0,
        }), ParameterType.RequestBody);
        var response = client.Execute(request);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Thread.Sleep(2000); // while developing we observed that the error message says to retry in 2s
            throw new Exception($"Too many requests - Call to openai returned HTTP {response.StatusCode}"); // hack the message must start with 'Too many requests' for the core to retry
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            executionContext.Log.LogError(response.Content);
            throw new Exception($"Call to openai returned HTTP {response.StatusCode}");
        }

        executionContext.Log.LogDebug($"Prompt\n{prompt}\n\nResponse\n{response.Content}");

        var data = JsonConvert.DeserializeObject<OpenAiChatCompletionResponse>(response.Content);

        if (data == null)
        {
            throw new Exception(
                $"openai returned HTTP {response.StatusCode} but {nameof(data)} was null");
        }

        var first = data.Choices.SafeEnumerate().FirstOrDefault();

        var content = first?.Message?.Content;

        if (content == null)
        {
            throw new Exception("openai returned null");
        }

        return content.TrimEnd();
    }

    public override IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context,
        IExternalSearchQueryResult result, IExternalSearchRequest request)
    {
        // Note: This needs to be cleaned up, but since config and provider is not used in GetPrimaryEntityPreviewImage this is fine.
        var dummyConfig = new Dictionary<string, object>();
        var dummyProvider = new DefaultExternalSearchProviderProvider(context.ApplicationContext, this);

        return GetPrimaryEntityPreviewImage(context, result, request, dummyConfig, dummyProvider);
    }

    private IEntityMetadata CreateMetadata(IExternalSearchQueryResult<JObject> resultItem,
        IExternalSearchRequest request)
    {
        var metadata = new EntityMetadataPart();

        PopulateMetadata(metadata, resultItem, request);

        return metadata;
    }

    private void PopulateMetadata(IEntityMetadata metadata, IExternalSearchQueryResult<JObject> resultItem,
        IExternalSearchRequest request)
    {
        metadata.EntityType = request.EntityMetaData.EntityType;
        metadata.Name = request.EntityMetaData.Name;
        metadata.OriginEntityCode = request.EntityMetaData.OriginEntityCode;

        using var en = resultItem.Data.GetEnumerator();
        while (en.MoveNext())
        {
            var key = en.Current.Key;
            key = Regex.Replace(key, "^output:vocabulary:", "", RegexOptions.IgnoreCase);
            metadata.Properties[key] = en.Current.Value.ToString();
        }
    }

    // Since this is a configurable external search provider, these methods should never be called
    public override bool Accepts(EntityType entityType)
    {
        throw new NotSupportedException();
    }

    public override IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context,
        IExternalSearchRequest request)
    {
        throw new NotSupportedException();
    }

    public override IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context,
        IExternalSearchQuery query)
    {
        throw new NotSupportedException();
    }

    public override IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query,
        IExternalSearchQueryResult result, IExternalSearchRequest request)
    {
        throw new NotSupportedException();
    }

    public override IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context,
        IExternalSearchQueryResult result, IExternalSearchRequest request)
    {
        throw new NotSupportedException();
    }
}
