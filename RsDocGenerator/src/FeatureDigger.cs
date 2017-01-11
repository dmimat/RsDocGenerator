using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.Application.Environment;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.Intentions.Scoped;
using JetBrains.ReSharper.Feature.Services.QuickFixes;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;

namespace RsDocGenerator
{
    public class FeatureDigger
    {
        private readonly IDataContext _myContext;
        private readonly List<Type> _inspectionTypesWithQuickFixes = new List<Type>();
        private readonly HighlightingSettingsManager _highlightingSettingsManager;
        private readonly FeatureCatalog _quickFixCatalog;
        private readonly FeatureCatalog _fixInScopeCatalog;
        private readonly FeatureCatalog _contexActionsCatalog;
        private readonly FeatureCatalog _contexActionInScopeCatalog;

        public FeatureDigger(IDataContext context)
        {
            _myContext = context;
            _highlightingSettingsManager = _myContext.GetComponent<HighlightingSettingsManager>();
            _contexActionsCatalog = DigContextActions();
            _quickFixCatalog = new FeatureCatalog(RsFeatureKind.QuickFix);
            _fixInScopeCatalog = new FeatureCatalog(RsFeatureKind.FixInScope);
            _contexActionInScopeCatalog = new FeatureCatalog(RsFeatureKind.ContextActionInScope);
            DigQuickFixes();
        }

        public FeatureCatalog GetConfigurableInspections()
        {
            var configInspectionsCatalog = new FeatureCatalog(RsFeatureKind.ConfigInspection);

            foreach (var inspection in _highlightingSettingsManager.SeverityConfigurations)
            {
                if (inspection.Internal && !Shell.Instance.IsInInternalMode) continue;

                List<string> langs = _highlightingSettingsManager.GetInspectionImplementations(inspection.Id)
                    .Select(l => l.Name)
                    .ToList();
                foreach (var language in langs)
                {
                    var feature = new RsFeature(inspection.Id, inspection.FullTitle, language, langs,
                        RsFeatureKind.ConfigInspection, inspection.DefaultSeverity, inspection.CompoundItemName,
                        inspection.GroupId);
                    configInspectionsCatalog.AddFeature(feature, language);
                }
            }
            return configInspectionsCatalog;
        }

        public FeatureCatalog DigContextActions()
        {
            var actionsCatalog = new FeatureCatalog(RsFeatureKind.ContextAction);
            foreach (var ca in _myContext.GetComponent<IContextActionTable>().AllActions)
            {
                var lang = ca.Group ?? "Unknown";
                var feature = new RsFeature(ca.ActionKey, ca.Name, lang, null,
                    RsFeatureKind.ContextAction, Severity.INFO, null, null);
                actionsCatalog.AddFeature(feature, lang);
            }
            return actionsCatalog;
        }
        public FeatureCatalog GetContextActions()
        {
            return _contexActionsCatalog;
        }

        public FeatureCatalog GetStaticInspections()
        {
            var staticInspectionsCatalog = new FeatureCatalog(RsFeatureKind.StaticInspection);
            var catalogs = _myContext.GetComponent<ShellPartCatalogSet>();
            foreach (var catalog in catalogs.Catalogs)
            {
                foreach (var part in catalog
                    .ApplyFilter(CatalogAttributeFilter<StaticSeverityHighlightingAttribute>.Instance)
                    .AllPartTypes)
                {
                    foreach (var attribute in part.GetPartAttributes<StaticSeverityHighlightingAttribute>())
                    {
                        var type = Type.GetType(part.AssemblyQualifiedName.ToRuntimeString());
                        if (type == null) continue;

                        // getting rid of generated TypeScript errors
                        if (type.Name.StartsWith("TS") && type.Name.EndsWith("Error") &&
                            !_inspectionTypesWithQuickFixes.Contains(type))
                            continue;

                        var staticSeverityAttribute = _highlightingSettingsManager.GetHighlightingAttribute(type)
                                as StaticSeverityHighlightingAttribute;
                        if (staticSeverityAttribute == null) continue;
                        if (staticSeverityAttribute.Severity == Severity.INFO) continue;
                        var languages = staticSeverityAttribute.Languages;
                        var groupId = staticSeverityAttribute.GroupId;
                        var langs = GetLangsFromHighlightingAttribute(languages, groupId);
                        var text = staticSeverityAttribute.ToolTipFormatString;
                        if (text.IsNullOrEmpty() || text.IsWhitespace() || text == "{0}")
                        {
                            text = type.Name.TextFromTypeName();
                        }
                        foreach (var lang in langs)
                        {
                            var feature = new RsFeature(type.Name, text, lang,
                                langs,
                                RsFeatureKind.StaticInspection, staticSeverityAttribute.Severity, null, groupId);
                            staticInspectionsCatalog.AddFeature(feature, lang);
                        }
                    }
                }
            }
            return staticInspectionsCatalog;
        }

        private void DigQuickFixes()
        {
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
                    if (type.IsInterface || type.IsAbstract) continue;
                    if (typeof(IQuickFix).IsAssignableFrom(type))
                    {
                        var inspectionTypes = _myContext.GetComponent<QuickFixTable>()
                            .GetHighlightingTypesForQuickFix(type);
                        if (!inspectionTypes.IsEmpty())
                        {
                            foreach (var inspectionType in inspectionTypes)
                                if (!_inspectionTypesWithQuickFixes.Contains(inspectionType))
                                    _inspectionTypesWithQuickFixes.Add(inspectionType);

                            AddQuickFixImplementations(type);
                        }
                    }
                    if (typeof(IContextAction).IsAssignableFrom(type) && typeof(IScopedAction).IsAssignableFrom(type) &&
                        !type.Name.EndsWith("Base"))
                    {
                        var action = _contexActionsCatalog.Features.FirstOrDefault(f => f.Id == type.FullName);
                        if (action != null)
                        {
                            var feature = new RsFeature(type.FullName, action.Text, action.Lang, action.Multilang, RsFeatureKind.ContextActionInScope, Severity.INFO, null,
                                null);
                            _contexActionInScopeCatalog.AddFeature(feature, action.Lang);
                        }

                    }
                }
            }
        }

        private void AddQuickFixImplementations(Type type)
        {
            string text;
            Type fixInScopeType = null;
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
            if (typeof(IScopedAction).IsAssignableFrom(type))
            {
                fixInScopeType = type;
                try
                {
                    text =
                        type.GetProperty("ScopedText")
                            .GetValue(FormatterServices.GetUninitializedObject(type), null)
                            .ToString();
                }
                catch (Exception)
                {
                }
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
                {
                    allLanguages.AddRange(GetLangsFromHighlightingAttribute(staticSeverityAttribute.Languages,
                        staticSeverityAttribute.GroupId));
                }
                var configurableAttribute = attribute as ConfigurableSeverityHighlightingAttribute;
                if (configurableAttribute != null)
                {
                    allLanguages.AddRange(GetLangsFromHighlightingAttribute(configurableAttribute.Languages, null));
                }
            }

            if (allLanguages.IsEmpty())
            {
                allLanguages.Add(GeneralHelpers.TryGetPsiLang(type.FullName));
            }

            foreach (var lang in allLanguages.Distinct().ToList())
            {
                var feature = new RsFeature(type.FullName, text, lang, allLanguages, RsFeatureKind.QuickFix, Severity.INFO,
                    null, null);
                _quickFixCatalog.AddFeature(feature, lang);
                if (fixInScopeType == null) continue;
                feature = new RsFeature(type.FullName, text, lang, allLanguages, RsFeatureKind.FixInScope, Severity.INFO, null,
                    null);
                _fixInScopeCatalog.AddFeature(feature, lang);
            }
        }

        private static List<string> GetLangsFromHighlightingAttribute(string langString, string groupId)
        {
            if (groupId != null && langString == null &&
                CodeInspectionHelpers.PsiLanguagesByCategoryNames.ContainsKey(groupId))
                langString = CodeInspectionHelpers.PsiLanguagesByCategoryNames[groupId];
            if (langString == null) return new List<string>();
            var langs = langString.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
            return langs.ToList();
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
    }
}