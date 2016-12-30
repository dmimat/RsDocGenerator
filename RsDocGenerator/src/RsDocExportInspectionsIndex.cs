using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Application.DataContext;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Daemon.OptionPages.Inspections.ViewModel.CodeInspectionSeverity;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Scope;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Settings;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Templates;
using JetBrains.ReSharper.LiveTemplates;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.UI.ActionsRevised;
using JetBrains.Util;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
    [Action("RsDocExportInspectionsIndex", "Export inpsections index", Id = 643759)]
    internal class RsDocExportInspectionsIndex : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var groupsByLanguage = new Dictionary<PsiLanguageType, InspectionByLanguageGroup>();

            foreach (var configurableSeverityItem in highlightingManager.SeverityConfigurations)
            {
                if (configurableSeverityItem.Internal && !Shell.Instance.IsInInternalMode) continue;

                foreach (var language in highlightingManager.GetInspectionImplementations(configurableSeverityItem.Id))
                {
                    InspectionByLanguageGroup languageGroup;
                    if (!groupsByLanguage.TryGetValue(language, out languageGroup))
                    {
                        groupsByLanguage[language] = languageGroup =
                            new InspectionByLanguageGroup(language.PresentableName);
                    }
                    languageGroup.AddInspection(configurableSeverityItem, highlightingManager);
                }
            }

            foreach (var languageGroup in groupsByLanguage)
            {
                languageGroup.Value.Sort();
                CreateIndpectionIndexTopic(languageGroup.Key.Name, languageGroup.Value, outputFolder);
            }

            return "Code inspections index";
        }

        private void CreateIndpectionIndexTopic(String language, InspectionByLanguageGroup languageGroup,
            string outputFolder)
        {
            var topicId = $"Reference__Code_Inspections_{language}";
            var fileName = Path.Combine(outputFolder, topicId + ".xml");
            var topic = XmlHelpers.CreateHmTopic(topicId);
            var topicRoot = topic.Root;
            var intro = XmlHelpers.CreateInclude("CA", "CodeInspectionIndexIntro");
            intro.Add(
                new XElement("var",
                    new XAttribute("name", "lang"),
                    new XAttribute("value", languageGroup.Name)),
                new XElement("var",
                    new XAttribute("name", "count"),
                    new XAttribute("value", languageGroup.TotalInspections())));

            topicRoot.Add(intro);

            var sortedCategories = languageGroup.Categories.OrderBy(o => o.Value.Name).ToList();

            foreach (var category in sortedCategories)
            {
                var count = category.Value.Inspections.Count;
                var chapter =
                    XmlHelpers.CreateChapter($"{category.Value.Name} ({count} {NounUtil.ToPluralOrSingular("inspection", count)})",
                        category.Key);
                chapter.Add(XmlHelpers.CreateInclude("Code_Analysis__Code_Inspections", category.Key));
                var summaryTable = XmlHelpers.CreateTable(new[] {"Inspection", "Default Severity"}, new[] {"80%", "20%"});
                foreach (var inspection in category.Value.Inspections)
                {

                    var compoundName = inspection.CompoundItemName ?? "not compound";
                    summaryTable.Add(
                        new XElement("tr",
                            new XElement("td", XmlHelpers.CreateHyperlink(inspection.FullTitle, inspection.Id, null),
                                new XComment(compoundName)),
                            new XElement("td", GetSeverityLink(inspection.DefaultSeverity))));
                }
                chapter.Add(summaryTable);
                topicRoot.Add(chapter);
            }

            topic.Save(fileName);
        }

        private static XElement GetSeverityLink(Severity inspectionDefaultSeverity)
        {
            switch (inspectionDefaultSeverity)
            {
                case Severity.DO_NOT_SHOW:
                    return XmlHelpers.CreateHyperlink("Disabled", "Code_Analysis__Configuring_Warnings", "disable");
                case Severity.ERROR:
                    return XmlHelpers.CreateHyperlink("Error", "Code_Analysis__Code_Inspections", "errors");
                case Severity.WARNING:
                    return XmlHelpers.CreateHyperlink("Warning", "Code_Analysis__Code_Inspections", "warnings");
                case Severity.SUGGESTION:
                    return XmlHelpers.CreateHyperlink("Suggestion", "Code_Analysis__Code_Inspections", "suggestions");
                case Severity.HINT:
                    return XmlHelpers.CreateHyperlink("Hint", "Code_Analysis__Code_Inspections", "hints");
                default:
                    return null;
            }
        }

        private class InspectionByLanguageGroup
        {
            public Dictionary<string, CategoryGroup> Categories { get; set; }
            public string Name { get; set; }

            public InspectionByLanguageGroup(string languagePresentableName)
            {
                Name = languagePresentableName;
                Categories = new Dictionary<string, CategoryGroup>();
            }

            public int TotalInspections()
            {
                return Categories.Values.Sum(category => category.Inspections.Count);
            }

            public void AddInspection(ConfigurableSeverityItem inspection,
                HighlightingSettingsManager highlightingManager)
            {
                var groupId = inspection.GroupId;
                CategoryGroup categoryGroup;
                if (!Categories.TryGetValue(groupId, out categoryGroup))
                {
                    foreach (var groupDescriptor in highlightingManager.ConfigurableGroups)
                    {
                        if (groupDescriptor.Key != groupId) continue;
                        Categories[groupId] = categoryGroup = new CategoryGroup(groupDescriptor.Title, new List<ConfigurableSeverityItem>());
                    }
                }
                categoryGroup.Inspections.Add(inspection);
            }

            public void Sort()
            {
                foreach (var category in Categories.Values)
                {
                    category.Inspections = category.Inspections.OrderBy(o => o.FullTitle).ToList();
                }
            }

            public class CategoryGroup
            {
                public CategoryGroup(string name, List<ConfigurableSeverityItem> inspections)
                {
                    if (name == null) throw new ArgumentNullException(nameof(name));
                    if (inspections == null) throw new ArgumentNullException(nameof(inspections));
                    Name = name;
                    Inspections = inspections;
                }

                public string Name { get; set; }
                public List<ConfigurableSeverityItem> Inspections { get; set; }
            }
        }
    }
}