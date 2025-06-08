using System.Text;

namespace SocialApp.WebApi.Infrastructure;

public sealed class MissingConfigurationException : Exception
{
    public MissingConfigurationException(string message, string key, IConfiguration configuration) 
        : base($"{message}\n{BuildMessage(key, configuration)}")
    {
    }
    
    private static string BuildMessage(string key, IConfiguration configuration)
    {
        var providers = (configuration as IConfigurationRoot)?.Providers.ToList();
        var sb = new StringBuilder();
        
        sb.AppendLine($"Could not find configuration key '{key}'.");
        sb.AppendLine("Registered configuration providers:");
        
        if (providers != null)
        {
            foreach (var provider in providers)
            {
                sb.AppendLine($" - {provider.GetType().Name}");
            }
        }
        else
        {
            sb.AppendLine(" - Unable to retrieve providers (not an IConfigurationRoot)");
        }
        
        sb.AppendLine("\nAvailable configuration keys:");
        AppendConfigurationKeys(configuration, sb, "");
        
        return sb.ToString();
    }
    
    private static void AppendConfigurationKeys(IConfiguration config, StringBuilder sb, string indent)
    {
        foreach (var child in config.GetChildren())
        {
            var value = child.Value;
            if (value != null)
            {
                sb.AppendLine($"{indent}{child.Path}:");
            }
            else
            {
                sb.AppendLine($"{indent}{child.Path}:");
                AppendConfigurationKeys(child, sb, indent + "  ");
            }
        }
    }
}