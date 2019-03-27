using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.Util;
using JetBrains.VsIntegration.Shell;
using Microsoft.CodeAnalysis.CodeFixes;
using Mono.Cecil;

namespace RsDocGenerator
{
    public class VsFeatureDigger
    {
        private readonly FeatureCatalog _configurableInspectionCatalog;
        private readonly FeatureCatalog _contexActionInScopeCatalog;
        private readonly FeatureCatalog _contexActionsCatalog;
        private readonly FeatureCatalog _fixInScopeCatalog;
        private readonly FeatureCatalog _inspectionWithQuickFixCatalog;
        private readonly FeatureCatalog _quickFixCatalog;
        private readonly FeatureCatalog _staticInspectionCatalog;


        public VsFeatureDigger(IDataContext context)
        {
            _quickFixCatalog = DigQuickFixes(context);
            _configurableInspectionCatalog = new FeatureCatalog(RsFeatureKind.ConfigInspection);
            _contexActionsCatalog = null;
            _fixInScopeCatalog = new FeatureCatalog(RsFeatureKind.FixInScope);
            _staticInspectionCatalog = new FeatureCatalog(RsFeatureKind.StaticInspection);
            _contexActionInScopeCatalog = new FeatureCatalog(RsFeatureKind.ContextActionInScope);
            _inspectionWithQuickFixCatalog = new FeatureCatalog(RsFeatureKind.InspectionWithQuickFix);
            DigInspections();
        }

        public FeatureCatalog GetQuickFixes()
        {
            return _quickFixCatalog;
        }

        public FeatureCatalog GetConfigurableInspections()
        {
            return _configurableInspectionCatalog;
        }

        public FeatureCatalog GetStaticInspections()
        {
            return _staticInspectionCatalog;
        }

        public FeatureCatalog GetContextActions()
        {
            return _contexActionsCatalog;
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

                foreach (var codeFixProvider in fixProviders)
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
                        RsFeatureKind.QuickFix, Severity.DO_NOT_SHOW, null, null, null,
                        codeFixProvider.FixableDiagnosticIds.ToList());

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

        private void DigInspections()
        {
            var path = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview";
            var files = Directory.GetFiles(path, "*CodeAnalysis*.dll", SearchOption.AllDirectories);
            var assemblies = files.Select(file => AssemblyDefinition.ReadAssembly(file)).ToList();

            foreach (var assembly in assemblies)
                try
                {
                    foreach (var type in assembly.Modules.SelectMany(md => md.GetTypes()))
                    {
                        if (type.Name.Contains("IDEDiagnosticIds"))
                        {
                            var language = "Common";
                            foreach (var field in type.Fields)
                            {
                                var added = false;
                                var isnpectionId = field.Constant.ToString();
                                var inspectionText = GetTextFromTypeName(field.Name, "DiagnosticId");

                                foreach (var fix in _quickFixCatalog.Features)
                                foreach (var id in fix.RelatedInspectionIds)
                                    if (isnpectionId == id)
                                    {
                                        language = fix.Lang;
                                        AddInspection(isnpectionId, inspectionText, language,
                                            _configurableInspectionCatalog);
                                        added = true;
                                    }

                                if (!added)
                                    AddInspection(isnpectionId, inspectionText, language,
                                        _configurableInspectionCatalog);
                            }
                        }

                        if (type.Name.Contains("XamlDiagnosticIds"))
                            foreach (var field in type.Fields)
                                AddInspection(field.Constant.ToString(),
                                    GetTextFromTypeName(field.Name, "Id"), "XAML",
                                    _configurableInspectionCatalog);

                        if (type.IsEnum && type.Name == "ErrorCode")
                            foreach (var field in type.Fields)
                            {
                                if (field.Name.Contains("WRN"))
                                    AddInspection(GetIdFromConstant(field),
                                        GetTextFromTypeName(field.Name, null, "WRN_"),
                                        "C#", _configurableInspectionCatalog);
                                if (field.Name.Contains("ERR"))
                                    AddInspection(GetIdFromConstant(field),
                                        GetTextFromTypeName(field.Name, null, "ERR_"),
                                        "C#", _staticInspectionCatalog);
                            }

                        if (type.IsEnum && type.Name == "ERRID")
                            foreach (var field in type.Fields)
                            {
                                var id = "BC" + field.Constant;
                                if (field.Name.Contains("WRN"))
                                    AddInspection(id, GetTextFromTypeName(field.Name, null, "WRN_"),
                                        "VB.NET", _configurableInspectionCatalog);
                                if (field.Name.Contains("ERR"))
                                    AddInspection(id, GetTextFromTypeName(field.Name, null, "ERR_"),
                                        "VB.NET", _staticInspectionCatalog);
                            }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("EXCEPTION:" + e);
                }
        }

        private string GetIdFromConstant(FieldDefinition field)
        {
            return "CS" + ((int) field.Constant).ToString("D4", CultureInfo.InvariantCulture);
        }

        private void AddInspection(string id, string text, string language, FeatureCatalog catalog)
        {
            var feature = new RsFeature(id, text, language, null,
                catalog.FeatureKind, Severity.DO_NOT_SHOW);

            if (catalog.Features.FirstOrDefault(f =>
                    f.Id == id && f.Lang == language) != null)
                return;
            catalog.AddFeature(feature, language);
        }

        private string GetTextFromTypeName(string typeName, string trimEnd = null, string trimStart = null)
        {
            if (trimEnd != null)
                typeName = typeName.TrimFromEnd(trimEnd);
            if (trimStart != null)
                typeName = typeName.TrimFromStart(trimStart);
            return string.Join(" ", Regex.Split(typeName, @"(?<!^)(?=[A-Z])")).ToLower();
        }
    }
}