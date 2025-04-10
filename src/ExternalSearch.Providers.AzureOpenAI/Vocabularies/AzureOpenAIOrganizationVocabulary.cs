// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AzureOpenAIOrganizationVocabulary.cs" company="Clued In">
//   Copyright Clued In
// </copyright>
// <summary>
//   Defines the AzureOpenAIOrganizationVocabulary type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using CluedIn.Core.Data.Vocabularies;

namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Vocabularies;

/// <summary>The AzureOpenAI organization vocabulary</summary>
/// <seealso cref="CluedIn.Core.Data.Vocabularies.SimpleVocabulary" />
// ReSharper disable once InconsistentNaming
public class AzureOpenAIOrganizationVocabulary : SimpleVocabulary
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AzureOpenAIOrganizationVocabulary" /> class.
    /// </summary>
    public AzureOpenAIOrganizationVocabulary()
    {
        VocabularyName = "AzureOpenAI Organization";
        KeyPrefix = "AzureOpenAI.organization";
        KeySeparator = ".";
        Grouping = Core.Data.EntityType.Organization;

        AddGroup("Metadata", group =>
        {
        });
    }
}
