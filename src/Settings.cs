using Microsoft.Extensions.Configuration;

namespace AgentsDemoSK;


/// Application settings
public class Settings
{
    private readonly IConfigurationRoot _configuration;

    /// Creates a new settings instance
    public Settings()
    {
        // Load configuration from appsettings.json and environment variables
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// Gets Azure OpenAI settings
    public AzureOpenAISettings AzureOpenAI => _configuration.GetSection("AzureOpenAISettings").Get<AzureOpenAISettings>() ?? new AzureOpenAISettings();
    
    /// Gets Azure Language settings
    public AzureLanguageSettings AzureLanguage => _configuration.GetSection("AzureLanguageSettings").Get<AzureLanguageSettings>() ?? new AzureLanguageSettings();
    
    /// Gets Azure Search settings
    public AzureSearchSettings AzureSearch => _configuration.GetSection("AzureSearchSettings").Get<AzureSearchSettings>() ?? new AzureSearchSettings();

    /// Gets settings for the specified type
    public T GetSettings<T>() where T : new()
    {
        T? settings = _configuration.GetSection(typeof(T).Name).Get<T>();
        return settings ?? new T();
    }
}

/// Azure OpenAI settings
public class AzureOpenAISettings
{
    /// Azure OpenAI endpoint
    public string Endpoint { get; set; } = string.Empty;
    
    /// Azure OpenAI model deployment name
    public string ModelDeploymentName { get; set; } = string.Empty;
    
    /// Azure OpenAI API key
    public string ApiKey { get; set; } = string.Empty;
    
    /// Azure OpenAI API version
    public string ApiVersion { get; set; } = string.Empty;
}

/// Azure Language settings
public class AzureLanguageSettings
{
    /// Azure Language endpoint
    public string Endpoint { get; set; } = string.Empty;
    
    /// Azure Language API key
    public string Key { get; set; } = string.Empty;
    
    /// Azure Language model deployment name
    public string ModelDeployment { get; set; } = string.Empty;
    
    /// Azure Language API version
    public string ApiVersion { get; set; } = string.Empty;
}

/// Azure Search settings
public class AzureSearchSettings
{
    /// Azure Search service endpoint
    public string ServiceEndpoint { get; set; } = string.Empty;
    
    /// Azure Search API key
    public string Key { get; set; } = string.Empty;
    
    /// Azure Search index name
    public string IndexName { get; set; } = string.Empty;
    
    /// Azure Search top-k results
    public int TopK { get; set; } = 5;
}