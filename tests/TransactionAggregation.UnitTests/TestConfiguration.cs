using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TransactionAggregation.UnitTests;

/// <summary>
/// Loads option values from the API <c>appsettings.json</c> so unit tests do not
/// re-hardcode settings that already live in configuration.
/// </summary>
internal static class TestConfiguration
{
    private static readonly Lazy<IConfigurationRoot> Root = new(Build);

    public static IConfiguration Configuration => Root.Value;

    public static T GetOptions<T>(string sectionName) where T : class, new()
    {
        var options = new T();
        Root.Value.GetSection(sectionName).Bind(options);
        return options;
    }

    public static IOptions<T> CreateOptions<T>(string sectionName) where T : class, new() =>
        Options.Create(GetOptions<T>(sectionName));

    private static IConfigurationRoot Build()
    {
        var basePath = AppContext.BaseDirectory;
        var appsettingsPath = Path.Combine(basePath, "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            throw new FileNotFoundException(
                "Expected API appsettings.json to be copied to the unit test output directory.",
                appsettingsPath);
        }

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }
}

