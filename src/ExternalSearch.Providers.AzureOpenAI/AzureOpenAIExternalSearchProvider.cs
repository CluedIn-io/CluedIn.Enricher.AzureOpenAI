using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using CluedIn.Core;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Data.Vocabularies;
using CluedIn.Core.ExternalSearch;
using CluedIn.Core.Providers;
using CluedIn.Crawling.Helpers;
using CluedIn.ExternalSearch.Provider;
using CluedIn.ExternalSearch.Providers.AzureOpenAI.Models;
using CluedIn.ExternalSearch.Providers.AzureOpenAI.Vocabularies;
using EntityType = CluedIn.Core.Data.EntityType;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI;

/// <summary>The Azure OpenAI external search provider.</summary>
/// <seealso cref="ExternalSearchProviderBase" />
// ReSharper disable once InconsistentNaming
public class AzureOpenAIExternalSearchProvider : ExternalSearchProviderBase, IExtendedEnricherMetadata,
    IConfigurableExternalSearchProvider, IExternalSearchProviderWithVerifyConnection
{
    /**********************************************************************************************************
     * FIELDS
     **********************************************************************************************************/

    private static readonly EntityType[] _defaultAcceptedEntityTypes = [EntityType.Organization];

    /**********************************************************************************************************
     * CONSTRUCTORS
     **********************************************************************************************************/

    public AzureOpenAIExternalSearchProvider()
        : base(Constants.ProviderId, _defaultAcceptedEntityTypes)
    {
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
        //TODO Andrew map the results here to clue
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

        using (context.Log.BeginScope("{0} {1}: query {2}, request {3}, result {4}", GetType().Name, "BuildClues",
                   query, request, result))
        {
            var resultItem = result.As<AzureOpenAIResponse>();
            var clue = new Clue(request.EntityMetaData.OriginEntityCode, context.Organization);

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
            var metadata = CreateMetadata(result.As<AzureOpenAIResponse>(), request);

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


    public ConnectionVerificationResult VerifyConnection(ExecutionContext context,
        IReadOnlyDictionary<string, object> config)
    {
        //TODO Update Test Connection
        IDictionary<string, object> configDict = config.ToDictionary(entry => entry.Key, entry => entry.Value);
        var jobData = new AzureOpenAIExternalSearchJobData(configDict);
        var client = new RestClient("URL");

        var azureOpenAiRequest = new AzureOpenAIRequest();

        var request = new RestRequest("data", Method.POST);
        request.AddHeader("Content-Type", "application/json");
        //request.AddHeader("ApiToken", jobData.ApiToken);
        request.AddJsonBody(azureOpenAiRequest);

        var response = client.ExecuteAsync<AzureOpenAIResponse>(request).Result;

        return ConstructVerifyConnectionResponse(response);
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
            if (string.IsNullOrEmpty(config.ApiToken))
            {
                context.Log.LogError("ApiToken for Azure OpenAI must be provided.");
                yield break;
            }

            if (!Accepts(config, request.EntityMetaData.EntityType))
            {
                context.Log.LogTrace("Unacceptable entity type from '{EntityName}', entity code '{EntityCode}'",
                    request.EntityMetaData.DisplayName, request.EntityMetaData.EntityType.Code);
                yield break;
            }

            context.Log.LogTrace("Starting to build queries for {EntityName}", request.EntityMetaData.DisplayName);

            var entityType = request.EntityMetaData.EntityType;

            var configMap = config.ToDictionary();

            //TODO Andrew Build the queries here
            //var responseVocabularyKey = GetValue(request, configMap, Constants.KeyName.ResponseVocabularyKey);

            //context.Log.LogInformation(
            //    "External search query produced, ExternalSearchQueryParameter: '{Identifier}' EntityType: '{EntityCode}' Value: '{SanitizedValue}'",
            //    ExternalSearchQueryParameter.Identifier, entityType.Code, value);

            //var queryDict = new Dictionary<string, string> { { "responseVocabularyKey", responseVocabularyKey.FirstOrDefault() } };
            var queryDict = new Dictionary<string, string>();

            yield return new ExternalSearchQuery(this, entityType, queryDict);

            context.Log.LogTrace("Finished building queries for '{Name}'", request.EntityMetaData.Name);
        }
    }

    private IEnumerable<IExternalSearchQueryResult> InternalExecuteSearch(ExecutionContext context,
        IExternalSearchQuery query, AzureOpenAIExternalSearchJobData jobData)
    {
        //TODO Andrew call OpenAI API here
        if (string.IsNullOrEmpty(jobData.ApiToken))
        {
            throw new InvalidOperationException("ApiToken for AzureOpenAI must be provided.");
        }

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
            context.Log.LogTrace("Starting external search for Id: '{Id}' QueryKey: '{QueryKey}'", query.Id,
                query.QueryKey);

            var client = new RestClient("URL");

            var searchCompany = ExecuteRequest(context, query, client);

            yield return new ExternalSearchQueryResult<AzureOpenAIResponse>(query, searchCompany);
        }
    }

    public override IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context,
        IExternalSearchQueryResult result, IExternalSearchRequest request)
    {
        // Note: This needs to be cleaned up, but since config and provider is not used in GetPrimaryEntityPreviewImage this is fine.
        var dummyConfig = new Dictionary<string, object>();
        var dummyProvider = new DefaultExternalSearchProviderProvider(context.ApplicationContext, this);

        return GetPrimaryEntityPreviewImage(context, result, request, dummyConfig, dummyProvider);
    }

    private static AzureOpenAIResponse ExecuteRequest(ExecutionContext context, IExternalSearchQuery query, RestClient client)
    {
            var azureOpenAiRequest = new AzureOpenAIRequest();

            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            //request.AddHeader("ApiToken", apiToken);
            request.AddJsonBody(azureOpenAiRequest);
            var response = client.ExecuteAsync<AzureOpenAIResponse>(request).Result;

        //    if (response.StatusCode == HttpStatusCode.OK)
        //    {
        //        if (response.Data != null && response.Data.SearchSummary.TotalRecordsFound > 0)
        //        {
        //            var data = response.Data?.Data?.FirstOrDefault();
        //            var name = data?.TryGetValue("NAME", out var value) is true ? value?.ToString() : string.Empty;
        //            var bvdNumber = data?.TryGetValue("BVD_ID_NUMBER", out var value1) is true ? value1?.ToString() : string.Empty;

        //            var diagnostic =
        //                $"External search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' produced results, CompanyName: '{name}'  BvDNumber: '{bvdNumber}'";

        //            context.Log.LogTrace(diagnostic);

        //            return response.Data;
        //        }
        //        else
        //        {
        //            var diagnostic =
        //                $"Failed external search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

        //            context.Log.LogError(diagnostic);

        //            var content = JsonConvert.DeserializeObject<dynamic>(response.Content);
        //            if (content.error != null)
        //            {
        //                throw new InvalidOperationException(
        //                    $"{content.error.info} - Type: {content.error.type} Code: {content.error.code}");
        //            }

        //            // TODO else do what with content ? ...
        //        }
        //    }
        //    else if (response.StatusCode == HttpStatusCode.NoContent ||
        //             response.StatusCode == HttpStatusCode.NotFound)
        //    {
        //        var diagnostic =
        //            $"External search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' produced no results - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

        //        context.Log.LogWarning(diagnostic);

        //        return null;
        //    }
        //    else if (response.ErrorException != null)
        //    {
        //        var diagnostic =
        //            $"External search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' produced no results - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

        //        context.Log.LogError(diagnostic, response.ErrorException);

        //        throw new AggregateException(response.ErrorException.Message, response.ErrorException);
        //    }
        //    else
        //    {
        //        var diagnostic =
        //            $"Failed external search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

        //        context.Log.LogError(diagnostic);

        //        throw new ApplicationException(diagnostic);
        //    }

        //    context.Log.LogTrace("Finished external search for Id: '{Id}' QueryKey: '{QueryKey}'", query.Id,
        //        query.QueryKey);
        //}

        return null;
    }

    private ConnectionVerificationResult ConstructVerifyConnectionResponse<T>(IRestResponse<T> response)
    {
        //TODO Update the test connection response
        try
        {
            var errorMessageBase =
                $"{Constants.ProviderName} returned \"{(int)response.StatusCode} {response.StatusDescription}\".";
            if (response.ErrorException != null)
            {
                return new ConnectionVerificationResult(false,
                    $"{errorMessageBase} {(!string.IsNullOrWhiteSpace(response.ErrorException.Message) ? response.ErrorException.Message : "This could be due to breaking changes in the external system")}.");
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                return new ConnectionVerificationResult(false,
                    $"{errorMessageBase} This could be due to invalid API key.");
            }

            var regex = new Regex(@"\<(html|head|body|div|span|img|p\>|a href)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
            var isHtml = regex.IsMatch(response.Content);

            if (response.IsSuccessful)
            {
                return new ConnectionVerificationResult(response.IsSuccessful, string.Empty);
            }

            var azureOpenAiErrorResponse = JsonConvert.DeserializeObject<AzureOpenAIResponse>(response.Content);
            //var formattedErrorMessage = string.Empty;

            //if (azureOpenAiErrorResponse.At != null)
            //{
            //    formattedErrorMessage =
            //        $"Error at: \"{azureOpenAiErrorResponse.At}\"";
            //}

            //if (azureOpenAiErrorResponse.Found is { Count: > 0 })
            //{
            //    formattedErrorMessage += $", Found: \"{azureOpenAiErrorResponse.Found.First().Value}\"";

            //}

            //if (azureOpenAiErrorResponse.Expect is { Count: > 0 })
            //{
            //    formattedErrorMessage += $", Expect: \"{azureOpenAiErrorResponse.Expect.First().Value}\"";
            //}

            //var errorMessage = azureOpenAiErrorResponse.At == null && azureOpenAiErrorResponse.Found == null && azureOpenAiErrorResponse.Expect == null || isHtml
            //        ? $"{errorMessageBase} This could be due to breaking changes in the external system."
            //        : $"{errorMessageBase} {formattedErrorMessage}.";

            var errorMessage = string.Empty;
            return new ConnectionVerificationResult(response.IsSuccessful, errorMessage);
        }
        catch (Exception ex)
        {
            return new ConnectionVerificationResult(false, ex.Message);
        }
    }

    private IEntityMetadata CreateMetadata(IExternalSearchQueryResult<AzureOpenAIResponse> resultItem,
        IExternalSearchRequest request)
    {
        var metadata = new EntityMetadataPart();

        PopulateMetadata(metadata, resultItem, request);

        return metadata;
    }

    private void PopulateMetadata(IEntityMetadata metadata, IExternalSearchQueryResult<AzureOpenAIResponse> resultItem,
        IExternalSearchRequest request)
    {
        //TODO Andrew map the properties here
        var data = resultItem.Data;

        metadata.EntityType = request.EntityMetaData.EntityType;
        metadata.Name = request.EntityMetaData.Name;
        metadata.OriginEntityCode = request.EntityMetaData.OriginEntityCode;

        //foreach (var kvp in data)
        //{
        //    var camelCaseKey = kvp.Key.Replace("_", " ").ToLowerInvariant().ToCamelCase();
        //    metadata.Properties[AzureOpenAIVocabulary.Organization.KeyPrefix + AzureOpenAIVocabulary.Organization.KeySeparator + camelCaseKey] = kvp.Value.PrintIfAvailable();
        //}

        //metadata.Properties[AzureOpenAIVocabulary.Organization.Postcode] = data.PostCode;
    }

    private static HashSet<string> GetValue(IExternalSearchRequest request, IDictionary<string, object> config, string keyName)
    {
        HashSet<string> value = [];
        if (config.TryGetValue(keyName, out var customVocabKey) && !string.IsNullOrWhiteSpace(customVocabKey?.ToString()))
        {
            value = request.QueryParameters.GetValue(customVocabKey.ToString(), []);
        }

        return value;
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
