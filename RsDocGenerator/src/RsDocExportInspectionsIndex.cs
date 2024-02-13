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
            outputFolder = outputFolder.AddGeneratedPath();
            var featureDigger = new FeatureDigger(context);
            var configurableInspections = featureDigger.GetConfigurableInspections();
            var staticInspections = featureDigger.GetStaticInspections();
            
            var sweaTopic = new HelpTopic("Solution_Wide_Inspections_Generated", "Solution-wide inspections", outputFolder);
            var sweaTable = XmlHelpers.CreateTable(new[] {"Inspection", "Language", "Default Severity"});
            
            foreach (var language in configurableInspections.Languages)
            {
                var configCategories = configurableInspections.GetFeaturesByCategories(language);
                if (configCategories.IsEmpty())
                    continue;
                var langPresentable = GeneralHelpers.GetPsiLanguagePresentation(language);
  
                var topic = new HelpTopic($"Reference__Code_Inspections_{language}", "Code Inspections in " + langPresentable, outputFolder + "\\CodeInspectionIndex");
                var iChunksTopic = new HelpTopic($"Inspection_chunks_{language}", "Inspection chunks for " + langPresentable, outputFolder + "\\CodeInspectionIndex");
    
                topic.Add(new XElement("var",
                    new XAttribute("name", "lang"),
                    new XAttribute("value", langPresentable)));
                var intro = XmlHelpers.CreateInclude("CA", "CodeInspectionIndexIntro");
                var errorCount = staticInspections.GetLangImplementations(language).Count;
                if (staticInspections.GetLangImplementations(language).Count < 2)
                    intro.Add(new XAttribute("use-filter", "empty"));
                intro.Add(XmlHelpers.CreateVariable("count",
                    configurableInspections.GetLangImplementations(language).Count.ToString()),
                    XmlHelpers.CreateVariable("errCount", errorCount.ToString()));

                // if (langPresentable.Equals("C++"))
                //     topicRoot.Add(GeneralHelpers.CppSupportNoteElement());

                topic.Add(intro);

                foreach (var category in configCategories)
                {
                    var count = category.Value.Count;
                    var categoryIdNormalized = category.Key.NormalizeStringForAttribute();
                    var chapter =
                        XmlHelpers.CreateChapter(
                            $"{FeatureCatalog.GetGroupTitle(category.Key)} ({count} {NounUtil.ToPluralOrSingular("inspection", count)})",
                            category.Key);
                    chapter.Add(XmlHelpers.CreateInclude("CA", "Category_" + categoryIdNormalized));
                    var summaryTable = new XElement("table", new XAttribute("id", "tbl_" + categoryIdNormalized));
                    summaryTable.Add(XmlHelpers.CreateInclude("CA", "tr_code_inspection_index_header"));
                    foreach (var inspection in category.Value)
                    {
                        var compoundName = inspection.CompoundName ?? "not compound";
                        var iChunk = XmlHelpers.CreateChunk(inspection.Id, false);
                        var iChunkHeaderTable = new XElement("table", new XAttribute("style", "none"));
                        iChunkHeaderTable.Add(new XComment("Name: " + inspection.Text));
                        iChunkHeaderTable.Add(new XComment("Compound name: " + compoundName));
                        iChunkHeaderTable.Add(new XElement("tr", 
                            XmlHelpers.CreateInclude("CA", "iChunks_category"),
                            new XElement("td",
                                FeatureCatalog.GetGroupTitle(category.Key))));
                        iChunkHeaderTable.Add(new XElement("tr",
                            XmlHelpers.CreateInclude("CA", "iChunks_iId"),
                            new XElement("td",
                                new XElement("code", inspection.Id))));
                        iChunkHeaderTable.Add(new XElement("tr",
                            XmlHelpers.CreateInclude("CA", "iChunks_ecId"),
                            new XElement("td",
                                new XElement("code", inspection.EditorConfigId))));
                        iChunkHeaderTable.Add(new XElement("tr",
                            XmlHelpers.CreateInclude("CA", "iChunks_severity"), 
                            new XElement("td",
                                GetSeverityLink(inspection.Severity))));
                        var supportedLangs = "";
                        foreach (var supportedLang in inspection.Multilang)
                        {
                            var supportedLangPresentable = GeneralHelpers.GetPsiLanguagePresentation(supportedLang);
                            if (!supportedLangs.IsEmpty())
                                supportedLangs += ", ";
                            supportedLangs += supportedLangPresentable;
                        }
                        iChunkHeaderTable.Add(new XElement("tr",
                            new XElement("td", "Language"),
                            new XElement("td", supportedLangs)));
                        
                        if (language == "CSHARP" || language == "ASPX" || language == "ASXX" ||
                            language == "JAVA_SCRIPT" || language == "VBASIC" || language == "XAML")
                            iChunkHeaderTable.Add(new XElement("tr",
                                XmlHelpers.CreateInclude("CA", "iChunks_swea"),
                                new XElement("td",
                                    inspection.SweaRequired ? "Yes" : "No")));
                        iChunk.Add(iChunkHeaderTable);
                        iChunk.Add(XmlHelpers.CreateInclude("CA", "tip_disable"));
                        iChunksTopic.Add(iChunk);
                        
                        summaryTable.Add(
                            new XElement("tr",
                                new XElement("td",
                                    XmlHelpers.CreateHyperlink(inspection.Text,
                                        CodeInspectionHelpers.TryGetStaticHref(inspection.Id), null, true),
                                    new XComment(compoundName),
                                    new XElement("if", new XAttribute("filter", "inspection_id"),
                                        new XElement("br"),
                                        new XElement("code", inspection.Id)),
                                    new XElement("if", new XAttribute("filter", "editorconfig_id"),
                                        new XElement("br"),
                                        new XElement("code", inspection.EditorConfigId))),
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
                    topic.Add(chapter);
                }
                topic.Save();
                iChunksTopic.Save();
            }

            sweaTable.Add(new XAttribute("id", "swea_table"));
            sweaTopic.Add(sweaTable);
            sweaTopic.Save();

            return "Code inspections index";
        }

        private static XElement GetSeverityLink(Severity inspectionDefaultSeverity)
        {
            switch (inspectionDefaultSeverity)
            {
                case Severity.DO_NOT_SHOW:
                    return XmlHelpers.CreateHyperlink("Disabled", "Code_Analysis__Configuring_Warnings", "disable",
                        true);
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