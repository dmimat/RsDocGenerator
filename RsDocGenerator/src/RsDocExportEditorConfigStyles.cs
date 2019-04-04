using System.Collections.Generic;
using System.Linq;
using JetBrains;
using JetBrains.Application;
using JetBrains.Application.Components;
using JetBrains.Application.DataContext;
using JetBrains.Application.Environment;
using JetBrains.Application.Extensibility;
using JetBrains.Application.Settings;
using JetBrains.Application.Settings.Implementation;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.DataFlow;
using JetBrains.Diagnostics;
using JetBrains.DocumentModel;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.OptionPages;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle.ViewModels;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.EditorConfig;
using JetBrains.Util;
using JetBrains.Util.dataStructures;

namespace RsDocGenerator
{
    [Action("RsDocExportEditorConfigStyles", "Export EditorConfig Styles", Id = 1897)]
    public class RsDocExportEditorConfigStyles : RsDocExportBase
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder.AddGeneratedPath());
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            return GenerateDocs(context, outputFolder.AddGeneratedPath() + "\\EditorConfig");
        }

        public static string GenerateDocs(IDataContext context, string path)
        {
            var solution = context.GetData(ProjectModelDataConstants.SOLUTION);
            if (solution == null) return "Open a solution to enable generation";

            Lifetime.Using(lifetime =>
            {
                var ecService = solution.GetComponent<IEditorConfigSchema>();
                var partsCatalogue = solution.GetComponent<ShellPartCatalogSet>();
                var container = new ComponentContainer(lifetime, "Inplace Format Container");
                var settingsStore = solution.GetComponent<SettingsStore>();
                var host = solution.GetComponent<IApplicationHost>();

                var contextBoundSettingsStoreLive =
                    settingsStore.BindToContextLive(lifetime, ContextRange.ApplicationWide);
                var documentFactory = solution.GetComponent<IInMemoryDocumentFactory>();
                var documentBefore = documentFactory.CreateSimpleDocumentFromText(string.Empty, "Preview doc before");
                var documentAfter = documentFactory.CreateSimpleDocumentFromText(string.Empty, "Preview doc after");

                container
                    .RegisterCatalog<FormattingSettingsPresentationComponentAttribute>(partsCatalogue)
                    .RegisterCatalog<CodePreviewPreparatorComponentAttribute>(partsCatalogue)
                    //.RegisterCatalog<OptionsComponentAttribute>(partsCatalogue)
                    .Register(lifetime)
                    .Register(contextBoundSettingsStoreLive)
                    .Register<ValueEditorViewModelFactory>()
                    .Register<SettingsToHide>()
                    //.Register<IndentStyleSettingsAvailabilityChecker>()
                    .Compose();

                var schemas = container.GetComponents<ICodeStylePageSchema>()
                    .OrderBy(schema => schema.GetType().FullName).ToList();

//                FileSystemPath.Parse(path).Combine("debug.log").WriteTextStreamDenyWrite(writer =>
//                {
//                    writer.WriteLine("Count = {0}", schemas.Count);
//                    for (int i = 0; i < schemas.Count; i++)
//                    {
//                        writer.WriteLine("{0}. {1}: {2}", i, schemas[i].GetType().FullName, schemas[i].PageName);
//                    }
//                });

                // To filter out duplicate entries for one setting
                var settingsToEntry = new Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>>();
                var excludedEntries = new HashSet<ICodeStyleEntry>();
                var excludedSchemas = new HashSet<ICodeStylePageSchema>();
                foreach (var schema in schemas)
                foreach (var entry in schema.Entries)
                    FillSettingsToEntryDictionary(entry, schema.Language, settingsToEntry, ecService);

                foreach (var schema in schemas)
                {
                    var excludeSchema = true;
                    foreach (var entry in schema.Entries)
                        if (!CalculateIfEntryShouldBeExcluded(entry, schema.Language, settingsToEntry, excludedEntries))
                            excludeSchema = false;

                    if (excludeSchema)
                        excludedSchemas.Add(schema);
                }

                var map = new OneToListMultimap<string, PropertyDescription>();
                foreach (var language in schemas.Select(schema => schema.Language)
                    .Distinct()
                    .OrderBy(it => it.PresentableName))
                foreach (var schema in schemas.Where(it => ReferenceEquals(it.Language, language)))
                {
                    if (excludedSchemas.Contains(schema)) continue;

                    var preparator = schema.GetCodePreviewPreparator();
                    if (preparator == null) continue;

                    EditorConfigXdoc.ProcessEntries(language, schema, path,
                        documentBefore, documentAfter, lifetime, settingsStore, contextBoundSettingsStoreLive,
                        ecService, settingsToEntry, excludedEntries, preparator, solution, map);
                }

                EditorConfigXdoc.CreateIndex(path, context, map, ecService);
                EditorConfigXdoc.CreateGeneralizedPropertiesTopic(path, host, map, ecService);
            });
            return "Editorconfig styles";
        }

        private static void FillSettingsToEntryDictionary(
            ICodeStyleEntry entry, KnownLanguage schemaLanguage,
            Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>> settingsToEntry,
            IEditorConfigSchema ecService)
        {
            var settingsEntry = entry.SettingsEntry;
            if (settingsEntry != null)
            {
                var propertyInfo = ecService.GetPropertiesForSettingsEntry(settingsEntry).FirstOrDefault();
                if (propertyInfo != null && !propertyInfo.HideThisProperty)
                {
                    var pair = settingsToEntry.GetValueSafe(settingsEntry);
                    if (pair.First == null)
                    {
                        settingsToEntry.Add(settingsEntry, Pair.Of(entry, schemaLanguage));
                    }
                    else
                    {
                        var oldLanguage = pair.Second;
                        if (!schemaLanguage.IsLanguage(oldLanguage))
                        {
                            Assertion.Assert(oldLanguage.IsLanguage(schemaLanguage),
                                "oldLanguage.IsLanguage(schemaLanguage)");
                            settingsToEntry[settingsEntry] = Pair.Of(entry, schemaLanguage);
                        }
                    }
                }
            }

            foreach (var child in entry.Children)
                FillSettingsToEntryDictionary(child, schemaLanguage, settingsToEntry, ecService);
        }

        private static bool CalculateIfEntryShouldBeExcluded(
            ICodeStyleEntry entry, KnownLanguage schemaLanguage,
            Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>> settingsToEntry,
            HashSet<ICodeStyleEntry> excludedEntries)
        {
            var excludeEntry = true;
            var settingsEntry = entry.SettingsEntry;
            if (settingsEntry != null)
            {
                var pair = settingsToEntry.GetValueSafe(settingsEntry);
                if (pair.First == entry)
                    excludeEntry = false;
            }

            foreach (var child in entry.Children)
                if (!CalculateIfEntryShouldBeExcluded(child, schemaLanguage, settingsToEntry, excludedEntries))
                    excludeEntry = false;

            if (excludeEntry)
                excludedEntries.Add(entry);
            return excludeEntry;
        }

        public class PropertyDescription
        {
            public string FileName { get; set; }
            public string Id { get; set; }
            public string SectionDescription { get; set; }
            public string Description { get; set; }
            public KnownLanguage Language { get; set; }
            public bool IsGeneralized { get; set; }
        }
    }
}