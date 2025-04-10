using System;
using System.Collections.Generic;
using System.Linq;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI;

public static class Constants
{
    public const string ComponentName = "AzureOpenAI";
    public const string ProviderName = "Azure OpenAI";

    public const string Instruction = """
                                      [
                                        {
                                          "type": "bulleted-list",
                                          "children": [
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the entity type to specify the golden records you want to enrich. Only golden records belonging to that entity type will be enriched."
                                                }
                                              ]
                                            },
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the vocabulary keys to provide the input for the enricher to search for additional information. For example, if you provide the website vocabulary key for the Web enricher, it will use specific websites to look for information about companies. In some cases, vocabulary keys are not required. If you don't add them, the enricher will use default vocabulary keys."
                                                }
                                              ]
                                            },
                                            {
                                              "type": "list-item",
                                              "children": [
                                                {
                                                  "text": "Add the API key to enable the enricher to retrieve information from a specific API. For example, the Azure OpenAI enricher requires an access key to authenticate with the Azure OpenAI API."
                                                }
                                              ]
                                            }
                                          ]
                                        }
                                      ]
                                      """;

    public static readonly Guid ProviderId = Guid.Parse("051e638f-747d-450c-9f4d-cf4a6deb0388");

    //TODO Update About Description
    public static string About { get; set; } =
        "Azure OpenAI";

    public static string Icon { get; set; } = "Resources.logo.svg";

    //TODO Update Domain url
    public static string Domain { get; set; } =
        "";

    public static IEnumerable<Control> Properties { get; set; } = new List<Control>
    {
        new()
        {
            DisplayName = "AI Deployment Name",
            Type = "input",
            IsRequired = true,
            Name = KeyName.AiDeployment,
            Help = "The Azure OpenAI Deployment Name.",
        },
        new()
        {
            DisplayName = "Accepted Entity Type",
            Type = "entityTypeSelector",
            IsRequired = true,
            Name = KeyName.AcceptedEntityType,
            Help = "The entity type that defines the golden records you want to enrich (e.g., /Organization)."
        },
        new()
        {
            DisplayName = "Prompt",
            Type = "input",
            IsRequired = true,
            Name = KeyName.Prompt,
            Help = "The prompt that will be passed to Azure OpenAI to generate results.",
        }
    };

    public static AuthMethods AuthMethods { get; set; } = new()
    {
        Token = new List<Control>().Concat(Properties)
    };

    public static Guide Guide { get; set; } = new() { Instructions = Instruction };

    public static IntegrationType IntegrationType { get; set; } = IntegrationType.Enrichment;

    public struct KeyName
    {
        public const string AcceptedEntityType = "acceptedEntityType";
        public const string AiDeployment = "aiDeploymentName";
        public const string Prompt = "prompt";
    }
}
