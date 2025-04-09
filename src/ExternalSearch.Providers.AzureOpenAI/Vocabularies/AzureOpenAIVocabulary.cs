namespace CluedIn.ExternalSearch.Providers.AzureOpenAI.Vocabularies;

/// <summary>The AzureOpenAI vocabulary</summary>
// ReSharper disable once InconsistentNaming
public static class AzureOpenAIVocabulary
{
    /// <summary>
    ///     Initializes static members of the <see cref="AzureOpenAIVocabulary" /> class.
    /// </summary>
    static AzureOpenAIVocabulary()
    {
        Organization = new AzureOpenAIOrganizationVocabulary();
    }

    /// <summary>Gets the organization.</summary>
    /// <value>The organization.</value>
    public static AzureOpenAIOrganizationVocabulary Organization { get; private set; }
}
