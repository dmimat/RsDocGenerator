using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Annotations;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.Application.Environment;
using JetBrains.Application.Settings;
using JetBrains.Reflection;
using JetBrains.ReSharper.Daemon.OptionPages.Inspections.ViewModel.CodeInspectionSeverity;
using JetBrains.ReSharper.Daemon.WebConfig.Highlightings;
using JetBrains.ReSharper.Daemon.Xaml.Highlightings;
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
using JetBrains.Util.dataStructures.Sources;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
    [Action("RsDocExportInspectionsIndex", "Export Code Inpsection Index", Id = 643759)]
    internal class RsDocExportInspectionsIndex : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            var featureKeeper = new FeatureKeeper(context);

            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var groupsByLanguage = new Dictionary<PsiLanguageType, InspectionByLanguageGroup>();

            foreach (var configurableSeverityItem in highlightingManager.SeverityConfigurations)
            {
                if (configurableSeverityItem.Internal && !Shell.Instance.IsInInternalMode) continue;

                List<PsiLanguageType> langs =  highlightingManager.GetInspectionImplementations(configurableSeverityItem.Id).ToList();
                foreach (var language in langs)
                    GetLanguageGroup(groupsByLanguage, language).AddConfigurableInspection(configurableSeverityItem, language, langs, highlightingManager);

            }

            var catalogs = Shell.Instance.GetComponent<ShellPartCatalogSet>();
            foreach (var catalog in catalogs.Catalogs)
            {
                foreach (var part in catalog.ApplyFilter(CatalogAttributeFilter<StaticSeverityHighlightingAttribute>.Instance).AllPartTypes)
                {
                    foreach (var attribute in part.GetPartAttributes<StaticSeverityHighlightingAttribute>())
                    {
                        var type = Type.GetType(part.AssemblyQualifiedName.ToRuntimeString());
                        if(type == null) continue;
                        var staticSeverityAttribute = highlightingManager.GetHighlightingAttribute(type) as StaticSeverityHighlightingAttribute;
                        if(staticSeverityAttribute == null) continue;
                        var languages = staticSeverityAttribute.Languages;
                        var groupId = staticSeverityAttribute.GroupId;

                        if (languages == null && CodeInspectionHelpers.PsiLanguagesByCategoryNames.ContainsKey(groupId))
                            languages = CodeInspectionHelpers.PsiLanguagesByCategoryNames[groupId];
                        if (languages == null) continue;
                        List<PsiLanguageType> psiLangs = new List<PsiLanguageType>();
                        foreach (var sLang in languages.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var lang = Shell.Instance.GetComponent<ILanguages>().GetLanguageByName(sLang);
                            if (lang == null)
                                continue;
                            psiLangs.Add(lang);
                        }
                        foreach (var psiLang in psiLangs)
                        {
                            GetLanguageGroup(groupsByLanguage, psiLang).AddStaticInspection(staticSeverityAttribute, groupId, psiLang, psiLangs, highlightingManager, type);
                        }
                    }
                }
            }


            foreach (var languageGroup in groupsByLanguage)
            {
                languageGroup.Value.Sort();
                CreateIndpectionIndexTopic(languageGroup.Key.Name, languageGroup.Value, outputFolder);
                featureKeeper.AddFeatures(languageGroup.Value, RsSmallFeatureKind.ConfigInspection);
                featureKeeper.AddFeatures(languageGroup.Value, RsSmallFeatureKind.StaticInspection);
            }

            featureKeeper.CloseSession();
            return "Code inspections index";
        }

        private static InspectionByLanguageGroup GetLanguageGroup(Dictionary<PsiLanguageType, InspectionByLanguageGroup> groupsByLanguage,
            PsiLanguageType language)
        {
            InspectionByLanguageGroup languageGroup;
            if (!groupsByLanguage.TryGetValue(language, out languageGroup))
            {
                groupsByLanguage[language] = languageGroup =
                    new InspectionByLanguageGroup(GeneralHelpers.GetPsiLanguagePresentation(language));
            }
            return languageGroup;
        }

        private void CreateIndpectionIndexTopic(String language, InspectionByLanguageGroup languageGroup,
            string outputFolder)
        {
            var topicId = string.Format("Reference__Code_Inspections_{0}", language);
            var fileName = Path.Combine(outputFolder, topicId + ".xml");
            var topic = XmlHelpers.CreateHmTopic(topicId);
            var topicRoot = topic.Root;
            var intro = XmlHelpers.CreateInclude("CA", "CodeInspectionIndexIntro");
            var errorCount = languageGroup.ErrorCount;
            if (languageGroup.ErrorCount < 2)
                intro.Add(new XAttribute("filter", "empty"));
            intro.Add(
                new XElement("var",
                    new XAttribute("name", "lang"),
                    new XAttribute("value", languageGroup.Name)),
                new XElement("var",
                    new XAttribute("name", "count"),
                    new XAttribute("value", languageGroup.TotalConfigurableInspections())),
                new XElement("var",
                    new XAttribute("name", "errCount"),
                    new XAttribute("value", errorCount)));

            topicRoot.Add(intro);
            if (languageGroup.Name.Equals("C++"))
            {
                topicRoot.Add(XmlHelpers.CreateInclude("Code_Analysis_in_CPP", "cpp_support_note"));
            }

            var sortedCategories = languageGroup.ConfigurableCategories.OrderBy(o => o.Value.Name).ToList();

            foreach (var category in sortedCategories)
            {
                var count = category.Value.Inspections.Count;
                var chapter =
                    XmlHelpers.CreateChapter(
                      string.Format("{0} ({1} {2})", category.Value.Name, count,
                        NounUtil.ToPluralOrSingular("inspection", count)),
                        category.Key);
                chapter.Add(XmlHelpers.CreateInclude("Code_Analysis__Code_Inspections", category.Key));
                var summaryTable = XmlHelpers.CreateTable(new[] {"Inspection", "Default Severity"},
                    new[] {"80%", "20%"});
                foreach (var inspection in category.Value.Inspections)
                {
                    var compoundName = inspection.CompoundName ?? "not compound";
                    summaryTable.Add(
                        new XElement("tr",
                            new XElement("td",
                                XmlHelpers.CreateHyperlink(inspection.Name, CodeInspectionHelpers.TryGetStaticHref(inspection.Id), null),
                                new XComment(compoundName)),
                            new XElement("td", GetSeverityLink(inspection.Severity))));
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

    }
}