using Microsoft.Extensions.Configuration;
using RsDocGenerator;

namespace VscodeGenerator;

public class Program
{
    private static string SettingsFile { get; set; } = string.Empty;
    private static string DocFolderPath { get; set; } = string.Empty;
    private static HelpTopic VscodeSettingsTopic { get; set; }

    static void Main(string[] args)
    {
        // Build configuration reading from appsettings.json in the root/output and allow CLI overrides.
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // Fallback: also try current working directory (useful when running from IDE without copying appsettings.json).
        var baseJson = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(baseJson))
        {
            var cwdJson = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            builder.AddJsonFile(cwdJson, optional: true, reloadOnChange: false);
        }
        
        var config = builder.Build();
        
        SettingsFile = config["SettingsFile"] ?? throw new Exception("SettingsFile not found in appsettings.json");

        DocFolderPath = config["DocFolder"] ?? throw new Exception("DocFolder not found in appsettings.json");
        
        VscodeSettingsTopic = new HelpTopic("VsCod_Settings", "VS Code settings", DocFolderPath);
        
    }
}