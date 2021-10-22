using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using JetBrains.Application.DataContext;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Daemon.CSharp.EditorConfig;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.Intentions.Scoped;
using JetBrains.ReSharper.Feature.Services.QuickFixes;
using JetBrains.ReSharper.Psi.EditorConfig;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;

namespace RsDocGenerator
{
    public class FeatureDigger
    {
        private readonly FeatureCatalog _configurableInspectionCatalog;
        private readonly FeatureCatalog _contexActionInScopeCatalog;
        private readonly FeatureCatalog _contexActionsCatalog;
        private readonly IEditorConfigSchema _editorConfigSchema;
        private readonly FeatureCatalog _fixInScopeCatalog;
        private readonly HighlightingSettingsManager _highlightingSettingsManager;
        private readonly FeatureCatalog _inspectionWithQuickFixCatalog;
        private readonly IDataContext _myContext;
        private readonly FeatureCatalog _quickFixCatalog;
        private readonly FeatureCatalog _staticInspectionCatalog;

        public FeatureDigger(IDataContext context)
        {
            _myContext = context;
            _highlightingSettingsManager = _myContext.GetComponent<HighlightingSettingsManager>();
            _editorConfigSchema = _myContext.GetComponent<IEditorConfigSchema>();
            _contexActionsCatalog = DigContextActions();
            _quickFixCatalog = new FeatureCatalog(RsFeatureKind.QuickFix);
            _fixInScopeCatalog = new FeatureCatalog(RsFeatureKind.FixInScope);
            _staticInspectionCatalog = new FeatureCatalog(RsFeatureKind.StaticInspection);
            _configurableInspectionCatalog = DigConfigurableInspections();
            _contexActionInScopeCatalog = new FeatureCatalog(RsFeatureKind.ContextActionInScope);
            _inspectionWithQuickFixCatalog = new FeatureCatalog(RsFeatureKind.InspectionWithQuickFix);
            DigFeaturesByTypes();
        }

        public FeatureCatalog GetConfigurableInspections()
        {
            return _configurableInspectionCatalog;
        }

        public FeatureCatalog GetContextActions()
        {
            return _contexActionsCatalog;
        }

        public FeatureCatalog GetStaticInspections()
        {
            return _staticInspectionCatalog;
        }

        public FeatureCatalog GetQuickFixes()
        {
            return _quickFixCatalog;
        }

        public FeatureCatalog GetFixesInScope()
        {
            return _fixInScopeCatalog;
        }

        public FeatureCatalog GetContextActionsInScope()
        {
            return _contexActionInScopeCatalog;
        }

        public FeatureCatalog GetInspectionsWithFixes()
        {
            return _inspectionWithQuickFixCatalog;
        }

        private FeatureCatalog DigConfigurableInspections()
        {
            var configInspectionsCatalog = new FeatureCatalog(RsFeatureKind.ConfigInspection);
            var solution = _myContext.GetData(ProjectModelDataConstants.SOLUTION);
            var styleCopRules = solution.GetComponent<StyleCopSettingsService>().GetSettingsWithAffectingRules();

            foreach (var inspection in _highlightingSettingsManager.SeverityConfigurations)
            {
/*                foreach (var copRule in styleCopRules)
                {
                    if (copRule.Key.First is SettingsIndexedEntry)
                    {
                        
                        if (copRule.Key.Second == inspection.Id)
                        {
                            Console.WriteLine(inspection.Id);                            
                        }                        
                    }
                }*/

                var swea = inspection.Id.Contains(".Global");
                if (inspection.Internal && !Shell.Instance.IsInInternalMode) continue;

                var langs = _highlightingSettingsManager.GetInspectionImplementations(inspection.Id)
                    .Select(l => l.Name)
                    .ToList();
                var overriddenLanguage = inspection.OverridenDisplayedLanguage;
                if (overriddenLanguage != null)
                {
                    langs.Clear();
                    langs.Add(overriddenLanguage);
                }

                foreach (var language in langs)
                {
                    var feature = new RsFeature(inspection.Id, inspection.FullTitle, language, langs,
                        RsFeatureKind.ConfigInspection, inspection.DefaultSeverity, inspection.CompoundItemName,
                        inspection.GroupId, _editorConfigSchema.GetPropertyNameForHighlightingId(inspection.Id), null,
                        swea);
                    if (configInspectionsCatalog.Features.FirstOrDefault(f =>
                            f.Id == inspection.Id && f.Lang == language) != null)
                        continue;

                    configInspectionsCatalog.AddFeature(feature, language);
                }

                // If description contains a hyperlink, add this link to CodeInspectionHelpers.ExternalInspectionLinks
                if (inspection.Description != null && inspection.Description.Contains("a href="))
                {
                    var regex = "href=\"(.*)\"";
                    var match = Regex.Match(inspection.Description, regex);
                    if (!match.Success) continue;
                    if (CodeInspectionHelpers.ExternalInspectionLinks.Keys.Contains(inspection.Id)) continue;
                    CodeInspectionHelpers.ExternalInspectionLinks.Add(inspection.Id, match.Groups[1].Value);
                }
            }

            return configInspectionsCatalog;
        }

        public FeatureCatalog DigContextActions()
        {
            var actionsCatalog = new FeatureCatalog(RsFeatureKind.ContextAction);
            foreach (var ca in _myContext.GetComponent<IContextActionTable>().AllActions)
            {
                var lang = GeneralHelpers.GetPsiLangByPresentation(ca.Group);
                var feature = new RsFeature(ca.ActionKey, ca.Name, lang, null, RsFeatureKind.ContextAction);
                actionsCatalog.AddFeature(feature, lang);
            }

            return actionsCatalog;
        }

        private void DigFeaturesByTypes()
        {
            var inspectionTypesWithQuickFixes = new List<Type>();
            var staticInspections = new Dictionary<Type, StaticSeverityHighlightingAttribute>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.IsInterface || type.IsAbstract || type.Name.EndsWith("Base")) continue;

                    // Quick-fixes
                    if (typeof(IQuickFix).IsAssignableFrom(type))
                    {
                        // Making sure that the quick-fix is registered for at least one inspection
                        var inspectionTypes = _myContext.GetComponent<QuickFixTable>()
                            .GetHighlightingTypesForQuickFix(type);
                        if (!inspectionTypes.IsEmpty())
                        {
                            // The list of inspections that have quick-fixes is used to drop some auto-generated inspections
                            foreach (var inspectionType in inspectionTypes)
                                if (!inspectionTypesWithQuickFixes.Contains(inspectionType))
                                    inspectionTypesWithQuickFixes.Add(inspectionType);

                            AddQuickFixImplementations(type);
                        }

                        continue;
                    }

                    // Context actions in scope
                    if (typeof(IContextAction).IsAssignableFrom(type) && typeof(IScopedAction).IsAssignableFrom(type))
                    {
                        var action = _contexActionsCatalog.Features.FirstOrDefault(f => f.Id == type.FullName);
                        if (action != null)
                        {
                            var feature = new RsFeature(type.FullName,
                                action.Text, action.Lang, action.Multilang, RsFeatureKind.ContextActionInScope);
                            _contexActionInScopeCatalog.AddFeature(feature, action.Lang);
                        }

                        continue;
                    }

                    // Static inspections
                    if (typeof(IHighlighting).IsAssignableFrom(type))
                    {
                        var staticSeverityAttribute = Attribute.GetCustomAttribute(type,
                            typeof(StaticSeverityHighlightingAttribute)) as StaticSeverityHighlightingAttribute;

                        // This is to get rid of configurable inspections that get StaticSeverityHighlightingAttribute
                        // from their base types
                        var configurableSeverityAttribute = Attribute.GetCustomAttribute(type,
                                typeof(ConfigurableSeverityHighlightingAttribute)) as
                            ConfigurableSeverityHighlightingAttribute;

                        if (staticSeverityAttribute != null &&
                            configurableSeverityAttribute == null &&
                            staticSeverityAttribute.Severity != Severity.INFO)
                            staticInspections.Add(type, staticSeverityAttribute);
                    }
                }
            }

            var uncountedStaticInspections = new List<Type>();
            foreach (var staticInspection in staticInspections)
            {
                var type = staticInspection.Key;
                var staticSeverityAttribute = staticInspection.Value;

                // getting rid of generated TypeScript errors
                if (type.Name.StartsWith("TS") && type.Name.EndsWith("Error") &&
                    !inspectionTypesWithQuickFixes.Contains(type))
                    continue;

                var groupId = staticSeverityAttribute.GroupId;
                if (groupId == "StructuralSearch")
                    continue;
                var langs = GetLangsFromHighlightingAttribute(staticSeverityAttribute.Languages, groupId);
                if (langs.Count == 0)
                {
                    if (type.FullName.Contains("Protobuf"))
                        langs.Add("Protobuf");
                    else if (type.FullName.Contains("UnresolvedPathError"))
                        langs.Add("Common");
                    else uncountedStaticInspections.Add(type);
                }

                var text = staticSeverityAttribute.ToolTipFormatString;
                if (text.IsNullOrEmpty() || text.IsWhitespace() || text == "{0}") text = type.Name.TextFromTypeName();
                foreach (var lang in langs)
                {
                    var feature = new RsFeature(type.FullName, text, lang, langs,
                        RsFeatureKind.StaticInspection, staticSeverityAttribute.Severity, null, groupId);
                    _staticInspectionCatalog.AddFeature(feature, lang);
                }
            }

            foreach (var inspectionType in inspectionTypesWithQuickFixes)
            {
                var staticInspectionFeature =
                    _staticInspectionCatalog.Features.FirstOrDefault(f => f.Id == inspectionType.FullName);
                if (staticInspectionFeature != null)
                {
                    _inspectionWithQuickFixCatalog.AddFeature(staticInspectionFeature, staticInspectionFeature.Lang);
                }
                else
                {
                    var configurableSeverityAttribute = Attribute.GetCustomAttribute(inspectionType,
                        typeof(ConfigurableSeverityHighlightingAttribute)) as ConfigurableSeverityHighlightingAttribute;
                    if (configurableSeverityAttribute != null)
                    {
                        var configInspectionFeatures =
                            _configurableInspectionCatalog.Features.Where(f =>
                                f.Id == configurableSeverityAttribute.ConfigurableSeverityId);
                        foreach (var configInspectionFeature in configInspectionFeatures)
                            _inspectionWithQuickFixCatalog.AddFeature(configInspectionFeature,
                                configInspectionFeature.Lang);
                    }
                }
            }
        }

        private void AddQuickFixImplementations(Type type)
        {
            string text;
            try
            {
                text =
                    type.GetProperty("Text")
                        .GetValue(FormatterServices.GetUninitializedObject(type), null)
                        .ToString();
            }
            catch (Exception)
            {
                text = type.Name.TextFromTypeName();
            }

            var inspectionTypes = _myContext.GetComponent<QuickFixTable>().GetHighlightingTypesForQuickFix(type);
            var allLanguages = new List<string>();
            foreach (var inspectionType in inspectionTypes)
            {
                HighlightingAttributeBase attribute;
                try
                {
                    attribute = _highlightingSettingsManager.GetHighlightingAttribute(inspectionType);
                }
                catch (Exception e)
                {
                    continue;
                }

                var staticSeverityAttribute = attribute as StaticSeverityHighlightingAttribute;
                if (staticSeverityAttribute != null)
                    allLanguages.AddRange(GetLangsFromHighlightingAttribute(staticSeverityAttribute.Languages,
                        staticSeverityAttribute.GroupId));
                var configurableAttribute = attribute as ConfigurableSeverityHighlightingAttribute;
                if (configurableAttribute != null)
                    allLanguages.AddRange(GetLangsFromHighlightingAttribute(configurableAttribute.Languages, null));
            }

            if (allLanguages.IsEmpty()) allLanguages.Add(GeneralHelpers.TryGetPsiLangFromTypeName(type.FullName));

            foreach (var lang in allLanguages.Distinct().ToList())
            {
                var feature = new RsFeature(type.FullName, text, lang, allLanguages, RsFeatureKind.QuickFix);
                _quickFixCatalog.AddFeature(feature, lang);
                if (!typeof(IScopedAction).IsAssignableFrom(type)) continue;
                feature = new RsFeature(type.FullName, text, lang, allLanguages, RsFeatureKind.FixInScope);
                _fixInScopeCatalog.AddFeature(feature, lang);
            }
        }

        private static List<string> GetLangsFromHighlightingAttribute(string langString, string groupId)
        {
            if (groupId != null && langString == null &&
                CodeInspectionHelpers.PsiLanguagesByCategoryNames.ContainsKey(groupId))
                langString = CodeInspectionHelpers.PsiLanguagesByCategoryNames[groupId];
            if (langString == null) return new List<string>();
            return langString.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}