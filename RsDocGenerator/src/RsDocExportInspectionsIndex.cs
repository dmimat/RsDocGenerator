using System.IO;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.UI.ActionsRevised;
using JetBrains.Util;

namespace RsDocGenerator
{
    [Action("RsDocExportInspectionsIndex", "Export Code Inpsection Index", Id = 643759)]
    internal class RsDocExportInspectionsIndex : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            var featureDigger = new FeatureDigger(context);
            var configurableInspetions = featureDigger.GetConfigurableInspections();
            var staticInspetions = featureDigger.GetStaticInspections();

            foreach (var language in configurableInspetions.Languages)
            {
                var configCategories = configurableInspetions.GetFeaturesByCategories(language);
                if (configCategories.IsEmpty())
                    continue;
                var langPresentable = GeneralHelpers.GetPsiLanguagePresentation(language);
                var topicId = string.Format("Reference__Code_Inspections_{0}", language);
                var fileName = Path.Combine(outputFolder, topicId + ".xml");
                var topic = XmlHelpers.CreateHmTopic(topicId, "Code Inspections in " + langPresentable);
                var topicRoot = topic.Root;
                var intro = XmlHelpers.CreateInclude("CA", "CodeInspectionIndexIntro");
                var errorCount = staticInspetions.GetLangImplementations(language).Count;
                if (staticInspetions.GetLangImplementations(language).Count < 2)
                    intro.Add(new XAttribute("filter", "empty"));
                intro.Add(
                    new XElement("var",
                        new XAttribute("name", "lang"),
                        new XAttribute("value", langPresentable)),
                    new XElement("var",
                            new XAttribute("name", "count"),
                            new XAttribute("value", configurableInspetions.GetLangImplementations(language).Count)),
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
                    var summaryTable = XmlHelpers.CreateTable(new[] {"Inspection", "Default Severity"},
                        new[] {"80%", "20%"});
                    foreach (var inspection in category.Value)
                    {
                        var compoundName = inspection.CompoundName ?? "not compound";
                        summaryTable.Add(
                            new XElement("tr",
                                new XElement("td",
                                    XmlHelpers.CreateHyperlink(inspection.Text,
                                        CodeInspectionHelpers.TryGetStaticHref(inspection.Id), null),
                                    new XComment(compoundName)),
                                new XElement("td", GetSeverityLink(inspection.Severity))));
                    }
                    chapter.Add(summaryTable);
                    topicRoot.Add(chapter);
                }
                topic.Save(fileName);
            }

            return "Code inspections index";
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
    }
}