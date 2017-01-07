using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.Application.Environment;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Resources.Shell;

namespace RsDocGenerator
{
    public class FeatureDigger
    {
        public static Dictionary<PsiLanguageType, FeaturesByLanguageGroup> GetConfigurableInspections(IDataContext context, RsFeatureKind featureKind)
        {
            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var groupsByLanguage = new Dictionary<PsiLanguageType, FeaturesByLanguageGroup>();

            foreach (var inspection in highlightingManager.SeverityConfigurations)
            {
                if (inspection.Internal && !Shell.Instance.IsInInternalMode) continue;

                List<PsiLanguageType> langs =  highlightingManager.GetInspectionImplementations(inspection.Id).ToList();
                foreach (var language in langs)
                {
                    var langGroup = GetLanguageGroup(groupsByLanguage, language, featureKind);
                    var feature = new RsFeature(inspection.Id, inspection.FullTitle, language, langs,
                        RsFeatureKind.ConfigInspection, inspection.DefaultSeverity, inspection.CompoundItemName);
                    var groupId = inspection.GroupId;
                    var groupName = groupId;
                    var configurableGroup = highlightingManager.ConfigurableGroups.FirstOrDefault(x => x.Key == groupId);
                    if (configurableGroup != null)
                        groupName = configurableGroup.Title;
                    langGroup.AddInspection(groupId, groupName, feature);
                }
            }

            return groupsByLanguage;
        }

        private static FeaturesByLanguageGroup GetLanguageGroup(Dictionary<PsiLanguageType, FeaturesByLanguageGroup> groupsByLanguage,
            PsiLanguageType language, RsFeatureKind featureKind)
        {
            FeaturesByLanguageGroup languageGroup;
            if (!groupsByLanguage.TryGetValue(language, out languageGroup))
            {
                groupsByLanguage[language] = languageGroup =
                    new FeaturesByLanguageGroup(GeneralHelpers.GetPsiLanguagePresentation(language), featureKind);
            }
            return languageGroup;
        }

        public static Dictionary<PsiLanguageType, FeaturesByLanguageGroup> GetStaticInspections(IDataContext context, RsFeatureKind featureKind)
        {
            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var groupsByLanguage = new Dictionary<PsiLanguageType, FeaturesByLanguageGroup>();
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
                        if(staticSeverityAttribute.Severity == Severity.INFO) continue;
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
                            var langGroup = GetLanguageGroup(groupsByLanguage, psiLang, featureKind);
                            var feature = new RsFeature(type.Name, staticSeverityAttribute.ToolTipFormatString, psiLang, psiLangs,
                                RsFeatureKind.StaticInspection, staticSeverityAttribute.Severity, null);
                            var groupName = groupId;
                            var staticGroup = highlightingManager.StaticGroups.FirstOrDefault(x => x.Key == groupId);
                            if (staticGroup != null)
                                groupName = staticGroup.Name;
                            langGroup.AddInspection(groupId, groupName, feature);
                        }
                    }
                }
            }
            return groupsByLanguage;
        }
    }
}