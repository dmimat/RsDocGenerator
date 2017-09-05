
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Application.DataContext;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Settings;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Templates;
using JetBrains.ReSharper.LiveTemplates;
using JetBrains.Util;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
  [JetBrains.Application.UI.ActionsRevised.Menu.Action("RsDocExportTemplates", "Export Templates", Id = 6759)]
  internal class RsDocExportTemplates : RsDocExportBase
  {
    private static string _templatesOutputFolder;

    public static string StartContentGeneration(IDataContext context, string outputFolder)
    {
      _templatesOutputFolder = outputFolder + "\\CodeTemplates";
      var bound = context.GetComponent<ISettingsStore>().BindToContextTransient(ContextRange.ApplicationWide);
      foreach (TemplateApplicability applicability in Enum.GetValues(typeof(TemplateApplicability)))
        CreateXml(applicability, bound, GeneralHelpers.GetCurrentVersion(), context);
      return "Code templates";
    }

    protected override string GenerateContent(IDataContext context, string outputFolder)
    {
      return StartContentGeneration(context, outputFolder);
    }

    private static void CreateXml(TemplateApplicability applicability,
      IContextBoundSettingsStore bound,
      string version, IDataContext context)
    {
      string type;
      ScopeFilter scopeFilter = ScopeFilter.Language;
      switch (applicability)
      {
        case TemplateApplicability.Live:
          type = "Live";
          break;
        case TemplateApplicability.Surround:
          type = "Surround";
          break;
        case TemplateApplicability.File:
          type = "File";
          scopeFilter = ScopeFilter.Project;
          break;
        default: return;
      }

      var topicId = "Reference__Templates_Explorer__" + type + "_Templates";
        var topicTitle = "Predefined " + type + " Templates";
        var fileName = Path.Combine(_templatesOutputFolder, topicId + ".xml");
      var topic = XmlHelpers.CreateHmTopic(topicId, topicTitle);
      var topicRoot = topic.Root;

      topicRoot.Add(new XElement("p",
        new XElement("menupath", "ReSharper | Tools | Templates Explorer | " + type + " Templates"),
        new XAttribute("product","rs")));

      topicRoot.Add(new XElement("p",
        "This section lists all predefined " + type + " templates in %product% %currentVersion%."));

      topicRoot.Add(new XElement("p", XmlHelpers.CreateInclude("Templates__Template_Basics__Template_Types", type, false)));
      var summaryTable = XmlHelpers.CreateTable(new[] {"Template", "Description"}, new[] {"20%", "80%"});
      var summaryItems = new List<Tuple<string, XElement>>();
      var tables = new Dictionary<string, XElement>();
      var defaultTemplates = context.GetComponent<StoredTemplatesProvider>()
            .EnumerateTemplates(bound, applicability, false);

      var myScopeCategoryManager = context.GetComponent<ScopeCategoryManager>();

      foreach (var template in defaultTemplates)
      {
        var templateIdPresentable = !template.Shortcut.IsNullOrEmpty() ? template.Shortcut : template.Description;
        templateIdPresentable = Regex.Replace(templateIdPresentable, "&Enum", "Enum");
        var currentTemplateLangs = new List<string>();
        var imported = String.Empty;
        var scopeString = String.Empty;
        var cat = String.Empty;

        foreach (var category in template.Categories)
        {
          if (category.Contains("Imported"))
          {
            imported = category;
            break;
          }
          if (category.Contains("C#")) cat = "C#";
          if (category.Contains("VB.NET")) cat = "VB.NET";
        }

        foreach (var point in template.ScopePoints)
        {
          foreach (var provider in myScopeCategoryManager.GetCoveredProviders(scopeFilter, point))
          {
            var lang = provider.CategoryCaption;
            if (lang == "ASP.NET" && type == "Surround" && !cat.IsNullOrEmpty()) { lang = lang + "(" + cat + ")"; }
            if (!lang.IsNullOrEmpty())
            {
              currentTemplateLangs.Add(lang);
              if (!tables.ContainsKey(lang)) {
                tables.Add(lang, XmlHelpers.CreateTable(new[] {"Template", "Details"}, new[] {"20%", "80%"}));
              }
            }
          }

          if (currentTemplateLangs.Count == 0)
          {
            MessageBox.ShowExclamation(String.Format("The '{0}' template has no associated languages",
              templateIdPresentable));
          }

          scopeString += " " + point.PresentableShortName + ",";
        }

        scopeString = scopeString.TrimEnd(',');

        var paramElement = new XElement("list");
        foreach (var param in template.Fields)
        {
          var expr = param.Expression as MacroCallExpressionNew;
          var paramItemElement = new XElement("li", new XElement("code", param.Name));
          if (expr != null)
          {
            var macro = MacroDescriptionFormatter.GetMacroAttribute(expr.Definition);
            paramItemElement.Add(" - " + macro.LongDescription + " (",
              XmlHelpers.CreateHyperlink(macro.Name, "Template_Macros", macro.Name, false),
              ")");
          }
          else
            paramItemElement.Add(" - " + "no macro");
          paramElement.Add(paramItemElement);
        }

        if (template.Text.Contains("$SELECTION$"))
          paramElement.Add(new XElement("li",
            new XElement("code", "SELECTION"), " - The text selected by the user before invoking the template."));

        if (template.Text.Contains("$END$"))
          paramElement.Add(new XElement("li",
            new XElement("code", "END"), " - The caret position after the template is applied."));

        var processedLangs = new List<string>();

        foreach (var lang in currentTemplateLangs)
        {
          if (!processedLangs.Contains(lang))
          {
            AddTemplateRow(tables, summaryItems, lang, templateIdPresentable, template, scopeString, paramElement, type,
              imported);
            processedLangs.Add(lang);
          }
        }
      }

      foreach (var table in tables)
      {
        summaryTable.Add(new XElement("tr",
          new XElement("td",
            new XAttribute("colspan", "2"),
            new XElement("b", table.Key))));

        foreach (var item in summaryItems)
        {
          if (item.Item1 == table.Key)
            summaryTable.Add(item.Item2);
        }

        var langForHeading = table.Key;
        if (langForHeading == "Global")
          langForHeading = "Global Usage";
        CreateTopicForLang(langForHeading, type, table.Value, version);
      }

      var indexChapter = XmlHelpers.CreateChapter(String.Format("Index of {0} Templates", type));
      topicRoot.Add(new XComment("Total " + type + " templates: " + summaryItems.Count));
      indexChapter.Add(summaryTable);
      topicRoot.Add(indexChapter);
      topic.Save(fileName);
    }

    private static void CreateTopicForLang(string lang, string type, XElement table, string version)
    {
      var topicId = CreateTopicIdForTypeAndLang(lang, type);
      var topicTitle = CreateTopicTitleForTypeAndLang(lang, type);
      var fileName = Path.Combine(_templatesOutputFolder, topicId + ".xml");
      var topic = XmlHelpers.CreateHmTopic(topicId, topicTitle);
      var topicRoot = topic.Root;

      topicRoot.Add(new XElement("p",
        new XElement("menupath", String.Format("ReSharper | Templates Explorer | {0} Templates | {1}", type, lang)),
        new XAttribute("product","rs")));

      string learnMoreTopic = "Templates__Applying_Templates";

      switch (type)
      {
        case "Live":
          learnMoreTopic = "Templates__Applying_Templates__Creating_Source_Code_Using_Live_Templates";
          break;
        case "Surround":
          learnMoreTopic = "Templates__Applying_Templates__Surrounding_Code_Fragments_with_Templates";
          break;
        case "File":
          learnMoreTopic = "Templates__Applying_Templates__Creating_Files_from_Templates";
          break;
      }

      topicRoot.Add(new XComment("Total: " + table.Elements().Count()));

      topicRoot.Add(new XElement("p",
        String.Format(
          "This topic lists all predefined {0} templates for {1} in %product% %currentVersion%. For more information about {0} templates, see ",
          type.ToLower(), lang, version),
        XmlHelpers.CreateHyperlink(null, learnMoreTopic, null, false)));
      topicRoot.Add(table);
      topic.Save(fileName);
    }

    private static string CreateTopicIdForTypeAndLang(string lang, string type)
    {
      return String.Format("Reference__Templates_Explorer__{0}_Templates_{1}", type, lang.NormalizeStringForAttribute());
    }

    private static string CreateTopicTitleForTypeAndLang(string lang, string type)
    {
      return  String.Format("Predefined {0} Templates for {1}", type, lang);
    }

    private static void AddTemplateRow(Dictionary<string, XElement> tables,
      List<Tuple<string, XElement>> summaryItems,
      string lang, string templateId, Template template,
      string scopeString, XElement paramElement, string type, string imported)
    {
      if (!imported.IsNullOrEmpty())
        imported = String.Format(" ({0})", imported);
      var templateIdFull = (type + "_" + templateId + "_" + lang).NormalizeStringForAttribute();


      var noDescriptionFallback = (template.Description.IsNullOrEmpty() || type == "File" || type == "Surround") &&
                                  !template.Description.Contains(' ')
        ? XmlHelpers.CreateInclude("TR", templateIdFull + "_desc", true)
        : template.Description as object;

      var paramHeader = new XElement("p");

      if (paramElement.HasElements)
        paramHeader.Add(new XElement("b", "Parameters "));

      tables[lang].Add(new XElement("tr",
        new XElement("td",
          new XElement("code", templateId), imported,
          new XAttribute("id", templateIdFull)),
        new XElement("td",
          new XElement("p", noDescriptionFallback),
          new XElement("p", new XElement("b", "Scope "), scopeString),
          new XElement("p", new XElement("b", "Body ")),
          XmlHelpers.CreateCodeBlock(template.Text, lang),
          paramHeader,
          paramElement,
          XmlHelpers.CreateInclude("TR", templateIdFull, true))));

      summaryItems.Add(
        new Tuple<string, XElement>(lang, new XElement("tr",
          new XElement("td",
            XmlHelpers.CreateHyperlink(templateId, CreateTopicIdForTypeAndLang(lang, type), templateIdFull, false), imported),
          new XElement("td", noDescriptionFallback))));
    }
  }
}