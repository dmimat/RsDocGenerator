using System.Text.Json;
using System.Xml.Linq;
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
        
        VscodeSettingsTopic = new HelpTopic("VsCode_Settings", "ReSharper settings in VS Code", DocFolderPath, false);
        try
        {
            // Ensure output directory exists
            if (!Directory.Exists(DocFolderPath))
            {
                Directory.CreateDirectory(DocFolderPath);
            }
            GenerateSettingsTopic();
            VscodeSettingsTopic.Save();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[VscodeGenerator] Failed to generate VS Code settings topic: {ex.Message}");
            throw;
        }
    }

    private static void GenerateSettingsTopic()
    {
        if (!File.Exists(SettingsFile))
            throw new FileNotFoundException("Settings file not found", SettingsFile);

        using var fs = File.OpenRead(SettingsFile);
        using var doc = JsonDocument.Parse(fs);

        // Find contributes.configuration (can be an object or an array)
        if (!doc.RootElement.TryGetProperty("contributes", out var contributes))
            throw new InvalidOperationException("The settings file does not contain 'contributes'.");

        if (!contributes.TryGetProperty("configuration", out var configuration))
            throw new InvalidOperationException("The settings file does not contain 'contributes.configuration'.");

        if (configuration.ValueKind == JsonValueKind.Array)
        {
            foreach (var cfg in configuration.EnumerateArray())
                ProcessConfigurationObject(cfg);
        }
        else if (configuration.ValueKind == JsonValueKind.Object)
        {
            ProcessConfigurationObject(configuration);
        }
        else
        {
            throw new InvalidOperationException("Unexpected JSON type for 'configuration'.");
        }
    }

    private static void ProcessConfigurationObject(JsonElement cfg)
    {
        // Title is used as chapter title; fallback to 'Settings'
        var title = cfg.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String
            ? titleEl.GetString() ?? "Settings"
            : "Settings";

        var chapter = XmlHelpers.CreateChapter(title);
        var table = XmlHelpers.CreateTwoColumnTable("ID", "Description", "50%");

        // properties: a map with setting id -> object with description, etc.
        if (cfg.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                var id = prop.Name;
                var desc = ExtractDescription(prop.Value);
                AddRow(table, id, desc);
            }
        }

        // Some schemas use 'allOf' to merge sub-schemas – handle basic case
        if (cfg.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var sub in allOf.EnumerateArray())
            {
                if (sub.ValueKind == JsonValueKind.Object &&
                    sub.TryGetProperty("properties", out var subProps) && subProps.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in subProps.EnumerateObject())
                    {
                        var id = prop.Name;
                        var desc = ExtractDescription(prop.Value);
                        AddRow(table, id, desc);
                    }
                }
            }
        }

        // Only add the table if it has any rows beyond the header
        if (table.Elements("tr").Skip(1).Any())
            chapter.Add(table);

        VscodeSettingsTopic.Add(chapter);
    }

    private static string ExtractDescription(JsonElement property)
    {
        // description can be string; sometimes markdownDescription is used; fallback to empty
        if (property.ValueKind == JsonValueKind.Object)
        {
            if (property.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
                return d.GetString() ?? string.Empty;
            if (property.TryGetProperty("markdownDescription", out var md) && md.ValueKind == JsonValueKind.String)
                return md.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static void AddRow(XElement table, string id, string description)
    {
        var tr = new XElement("tr");
        tr.Add(new XElement("td", id));
        tr.Add(new XElement("td", description));
        table.Add(tr);
    }
}