using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.Intentions.Scoped;
using JetBrains.ReSharper.Feature.Services.QuickFixes;
using JetBrains.Util;
using JetBrains.VsIntegration.Shell;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace RsDocGenerator
{
    public class VsFeatureDigger
    {
        private readonly IDataContext _myContext;
        private readonly HighlightingSettingsManager _highlightingSettingsManager;
        private readonly FeatureCatalog _quickFixCatalog;
        private readonly FeatureCatalog _fixInScopeCatalog;
        private readonly FeatureCatalog _contexActionsCatalog;
        private readonly FeatureCatalog _contexActionInScopeCatalog;
        private readonly FeatureCatalog _staticInspectionCatalog;
        private readonly FeatureCatalog _configurableInspectionCatalog;
        private readonly FeatureCatalog _inspectionWithQuickFixCatalog;


        public VsFeatureDigger(IDataContext context)
        {
            _myContext = context;
            _quickFixCatalog = DigQuickFixes(context);
            _configurableInspectionCatalog = DigConfigurableInspections();
            _contexActionsCatalog = null;
            _fixInScopeCatalog = new FeatureCatalog(RsFeatureKind.FixInScope);
            _staticInspectionCatalog = new FeatureCatalog(RsFeatureKind.StaticInspection);
            
            _contexActionInScopeCatalog = new FeatureCatalog(RsFeatureKind.ContextActionInScope);
            _inspectionWithQuickFixCatalog = new FeatureCatalog(RsFeatureKind.InspectionWithQuickFix);
            //DigFeaturesByTypes();
        }

        public FeatureCatalog GetQuickFixes() => _quickFixCatalog;
        public FeatureCatalog GetConfigurableInspections() => _configurableInspectionCatalog;
        public FeatureCatalog GetContextActions() => _contexActionsCatalog;
        public FeatureCatalog GetStaticInspections() => _staticInspectionCatalog;
        
        public FeatureCatalog GetFixesInScope() => _fixInScopeCatalog;
        public FeatureCatalog GetContextActionsInScope() => _contexActionInScopeCatalog;
        public FeatureCatalog GetInspectionsWithFixes() => _inspectionWithQuickFixCatalog;

        private FeatureCatalog DigQuickFixes(IDataContext context)
        {
            var quickFixCatalog = new FeatureCatalog(RsFeatureKind.QuickFix);
            try
            {
                var componentModel = context.TryGetComponent<VsComponentModelWrapper>();
                if (componentModel == null)
                {
                    MessageBox.ShowError("No component model");
                    return null;
                }

                var fixProviders = componentModel.GetExtensions<CodeFixProvider>().ToList();
                
                foreach (CodeFixProvider codeFixProvider in fixProviders)
                {
                    var typeName = codeFixProvider.GetType().Name;
                    var id = codeFixProvider.GetType().FullName;
                
                    var language = "Common";
                    if (id.Contains("CSharp"))
                    {
                        language = "C#";
                        typeName = typeName.TrimFromStart("CSharp");
                    }

                    if (id.Contains("VisualBasic"))
                    {
                        language = "VB.NET";
                        typeName = typeName.TrimFromStart("VisualBasic");
                    }  
                    
                    if (id.Contains("Xaml"))
                    {
                        language = "XAML";
                        typeName = typeName.TrimFromStart("Xaml");
                    }               
                    
                    if (id.Contains("FSharp"))
                    {
                        language = "F#";
                        typeName = typeName.TrimFromStart("FSharp");
                    }

                    var qfText = GetTextFromTypeName(typeName, "CodeFixProvider");
                    
                    var feature = new RsFeature(id, qfText, language, null,
                        RsFeatureKind.QuickFix, Severity.DO_NOT_SHOW,null,null,null, codeFixProvider.FixableDiagnosticIds.ToList());
                                
                    if (quickFixCatalog.Features.FirstOrDefault(f =>
                            f.Id == id && f.Lang == language) != null)
                        continue;
                    quickFixCatalog.AddFeature(feature, language);
                }

            }
            catch (Exception ex)
            {
                MessageBox.ShowError("Try to open an F# project and then run again");
                MessageBox.ShowError(ex.ToString());
            }

            return quickFixCatalog;
        }

        private FeatureCatalog DigConfigurableInspections()
        {
            var configInspectionsCatalog = new FeatureCatalog(RsFeatureKind.ConfigInspection);

            var path = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\";
            var files = Directory.GetFiles(path, "*CodeAnalysis*.dll", SearchOption.AllDirectories);
            var assemblies = files.Select(file => AssemblyDefinition.ReadAssembly(file)).ToList();

            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.Modules.SelectMany(md => md.GetTypes()))
                    {
                        if (type.Name.Contains("IDEDiagnosticIds"))
                        {
                            var language = "Common";
                            foreach (var field in type.Fields)
                            {
                                bool added = false;
                                var isnpectionId = field.Constant.ToString();
                                var inspectionText = GetTextFromTypeName(field.Name, "DiagnosticId");

                                foreach (var fix in _quickFixCatalog.Features)
                                {
                                    foreach (var id in fix.RelatedInspectionIds)
                                    {
                                        if (isnpectionId == id)
                                        {
                                            language = fix.Lang;
                                            AddInspection(isnpectionId, inspectionText, language,
                                                configInspectionsCatalog);
                                            added = true;
                                        }
                                    }
                                }
                                if(!added)
                                    AddInspection(isnpectionId, inspectionText, language, configInspectionsCatalog);
                            }
                        }
                        
                        if (type.Name.Contains("XamlDiagnosticIds"))
                            foreach (var field in type.Fields)
                                AddInspection(field.Constant.ToString(),
                                    GetTextFromTypeName(field.Name, "Id"), "XAML",
                                    configInspectionsCatalog);

                        if (type.IsEnum && type.Name == "ErrorCode")
                            foreach (var field in type.Fields)
                                if (field.Name.Contains("WRN"))
                                    AddInspection(
                                        "CS" + ((int) field.Constant).ToString("D4", CultureInfo.InvariantCulture),
                                        GetTextFromTypeName(field.Name, null, "WRN_"),
                                        "C#", configInspectionsCatalog);

                        if (type.IsEnum && type.Name == "ERRID")
                            foreach (var field in type.Fields)
                                if (field.Name.Contains("WRN"))
                                    AddInspection(
                                        "BC" + field.Constant,
                                        GetTextFromTypeName(field.Name, null, "WRN_"),
                                        "VB.NET", configInspectionsCatalog);

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("EXCEPTION:" + e);
                }
            }

            return configInspectionsCatalog;
        }


        private void AddInspection(string id, string text, string language, FeatureCatalog configInspectionsCatalog)
        {
            var feature = new RsFeature(id, text, language, null,
                RsFeatureKind.ConfigInspection, Severity.DO_NOT_SHOW);

            if (configInspectionsCatalog.Features.FirstOrDefault(f =>
                    f.Id == id && f.Lang == language) != null)
                return;
            configInspectionsCatalog.AddFeature(feature, language);
        }

        private string GetTextFromTypeName(string typeName, string trimEnd = null, string trimStart = null)
        {
            if (trimEnd != null)
                typeName = typeName.TrimFromEnd(trimEnd);
            if (trimStart != null)
                typeName = typeName.TrimFromStart(trimStart);
            return string.Join(" ", Regex.Split(typeName, @"(?<!^)(?=[A-Z])")).ToLower();
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
          

            if (allLanguages.IsEmpty())
            {
                allLanguages.Add(GeneralHelpers.TryGetPsiLangFromTypeName(type.FullName));
            }

            foreach (var lang in allLanguages.Distinct().ToList())
            {
                var feature = new RsFeature(type.FullName, text, lang, allLanguages, RsFeatureKind.QuickFix);
                //_quickFixCatalog.AddFeature(feature, lang);
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