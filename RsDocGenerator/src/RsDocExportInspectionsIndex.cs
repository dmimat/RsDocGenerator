using System.IO;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.Util;

namespace RsDocGenerator
{
    [Action("RsDocExportInspectionsIndex", "Export Code Inspection Index", Id = 643759)]
    internal class RsDocExportInspectionsIndex : RsDocExportBase, IAction
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            outputFolder = outputFolder.GetGeneratedDocsFolder();
            var featureDigger = new FeatureDigger(context);
            var configurableInspections = featureDigger.GetConfigurableInspections();
            var staticInspections = featureDigger.GetStaticInspections();

            const string sweaTopicId = "Solution_Wide_Inspections_Generated";
            var sweaFileName = Path.Combine(outputFolder, sweaTopicId + ".xml");
            var sweaTopic = XmlHelpers.CreateHmTopic(sweaTopicId, "Solution-Wide Inspections");
            var sweaTable = XmlHelpers.CreateTable(new[] {"Inspection", "Language", "Default Severity"});

            foreach (var language in configurableInspections.Languages)
            {
                var configCategories = configurableInspections.GetFeaturesByCategories(language);
                if (configCategories.IsEmpty())
                    continue;
                var langPresentable = GeneralHelpers.GetPsiLanguagePresentation(language);
                var topicId = $"Reference__Code_Inspections_{language}";
                var fileName = Path.Combine(outputFolder + "\\CodeInspectionIndex", topicId + ".xml");
                var topic = XmlHelpers.CreateHmTopic(topicId, "Code Inspections in " + langPresentable);
                var topicRoot = topic.Root;
                var intro = XmlHelpers.CreateInclude("CA", "CodeInspectionIndexIntro");
                var errorCount = staticInspections.GetLangImplementations(language).Count;
                if (staticInspections.GetLangImplementations(language).Count < 2)
                    intro.Add(new XAttribute("filter", "empty"));
                intro.Add(
                    new XElement("var",
                        new XAttribute("name", "lang"),
                        new XAttribute("value", langPresentable)),
                    new XElement("var",
                        new XAttribute("name", "count"),
                        new XAttribute("value", configurableInspections.GetLangImplementations(language).Count)),
                    new XElement("var",
                        new XAttribute("name", "errCount"),
                        new XAttribute("value", errorCount)));

                topicRoot.Add(intro);

                if (langPresentable.Equals("C++"))
                    topicRoot.Add(XmlHelpers.CreateInclude("Code_Analysis_in_CPP", "cpp_support_note"));

                foreach (var category in configCategories)
                {
                    var count = category.Value.Count;
                    var chapter =
                        XmlHelpers.CreateChapter(
                            string.Format("{0} ({1} {2})", FeatureCatalog.GetGroupTitle(category.Key), count,
                                NounUtil.ToPluralOrSingular("inspection", count)),
                            category.Key);
                    chapter.Add(XmlHelpers.CreateInclude("Code_Analysis__Code_Inspections", category.Key));
                    var summaryTable = new XElement("table");
                    summaryTable.Add(XmlHelpers.CreateInclude("CA", "tr_code_inspection_index_header"));
                    foreach (var inspection in category.Value)
                    {
                        var compoundName = inspection.CompoundName ?? "not compound";
                        summaryTable.Add(
                            new XElement("tr",
                                new XElement("td",
                                    XmlHelpers.CreateHyperlink(inspection.Text,
                                        CodeInspectionHelpers.TryGetStaticHref(inspection.Id), null, true),
                                    new XComment(compoundName)),
                                new XElement("td", new XElement("code", inspection.Id)),
                                new XElement("td", new XElement("code", inspection.EditorConfigId)),
                                new XElement("td", GetSeverityLink(inspection.Severity))));
                        if (inspection.SweaRequired)
                            sweaTable.Add(
                                new XElement("tr",
                                    new XElement("td",
                                        XmlHelpers.CreateHyperlink(inspection.Text, inspection.Id, null, true)),
                                    new XElement("td", langPresentable),
                                    new XElement("td", GetSeverityLink(inspection.Severity))));
                    }

                    chapter.Add(summaryTable);
                    topicRoot.Add(chapter);
                }

                topic.Save(fileName);
            }

            sweaTable.Add(new XAttribute("include-id", "swea_table"));
            sweaTopic.Root.Add(sweaTable);
            sweaTopic.Save(sweaFileName);

            return "Code inspections index";
        }

        private static XElement GetSeverityLink(Severity inspectionDefaultSeverity)
        {
            switch (inspectionDefaultSeverity)
            {
                case Severity.DO_NOT_SHOW:
                    return XmlHelpers.CreateHyperlink("Disabled", "Code_Analysis__Configuring_Warnings", "disable",
                        false);
                case Severity.ERROR:
                    return XmlHelpers.CreateHyperlink("Error", "Code_Analysis__Code_Inspections", "errors", false);
                case Severity.WARNING:
                    return XmlHelpers.CreateHyperlink("Warning", "Code_Analysis__Code_Inspections", "warnings", false);
                case Severity.SUGGESTION:
                    return XmlHelpers.CreateHyperlink("Suggestion", "Code_Analysis__Code_Inspections", "suggestions",
                        false);
                case Severity.HINT:
                    return XmlHelpers.CreateHyperlink("Hint", "Code_Analysis__Code_Inspections", "hints", false);
                default:
                    return null;
            }
        }
    }
}