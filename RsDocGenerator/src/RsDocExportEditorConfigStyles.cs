using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using JetBrains;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Components;
using JetBrains.Application.DataContext;
using JetBrains.Application.Environment;
using JetBrains.Application.Extensibility;
using JetBrains.Application.Settings;
using JetBrains.Application.Settings.Implementation;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.OptionPages;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle.ViewModels;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.EditorConfig;
using JetBrains.ReSharper.Psi.impl.EditorConfig;
using JetBrains.Util;
using JetBrains.Util.dataStructures;

namespace RsDocGenerator
{
    [Action("RsDocExportEditorConfigStyles", "Export EditorConfig Styles", Id = 1897)]
    public class RsDocExportEditorConfigStyles : RsDocExportBase
    {
        private const string GeneralizedPropsFileName = "EditorConfig_Generalized";

        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder + "\\topics\\ReSharper\\Generated");
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            var solution = context.GetData(ProjectModelDataConstants.SOLUTION);
            if (solution == null) return "Open a solution to enable generation";


            GenerateDocs(solution, outputFolder + "\\EditorConfig");


            return "Editorconfig styles";
        }

        public static void GenerateDocs(ISolution solution, string path)
        {
            Lifetimes.Using(lifetime =>
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
                    .Compose();

                var schemas = container.GetComponents<ICodeStylePageSchema>()
                    .OrderBy(schema => schema.GetType().FullName);

                // To filter out duplicate entries for one setting
                var settingsToEntry = new Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>>();
                var excludedEntries = new HashSet<ICodeStyleEntry>();
                var excludedSchemas = new HashSet<ICodeStylePageSchema>();
                foreach (var schema in schemas)
                {
                    foreach (var entry in schema.Entries)
                    {
                        FillSettingsToEntryDictionary(entry, schema.Language, settingsToEntry, ecService);
                    }
                }

                foreach (var schema in schemas)
                {
                    bool excludeSchema = true;
                    foreach (var entry in schema.Entries)
                    {
                        if (!CalculateIfEntryShouldBeExcluded(entry, schema.Language, settingsToEntry, excludedEntries))
                        {
                            excludeSchema = false;
                        }
                    }

                    if (excludeSchema)
                    {
                        excludedSchemas.Add(schema);
                    }
                }

                var map = new OneToListMultimap<string, PropertyDescription>();
                foreach (var language in schemas.Select(schema => schema.Language)
                    .Distinct()
                    .OrderBy(it => it.PresentableName))
                {
                    foreach (var schema in schemas.Where(it => ReferenceEquals(it.Language, language)))
                    {
                        if (excludedSchemas.Contains(schema)) continue;

                        var preparator = schema.GetCodePreviewPreparator();
                        if (preparator == null) continue;

                        var fileName = "EditorConfig_" + language.Name + "_" + schema.GetType().Name;
                        using (var writer = StartDocument(path, host, fileName,
                            "{0} - {1}".FormatEx(language.PresentableName, schema.PageName)))
                        {
                            foreach (var entry in schema.Entries)
                            {
                                ProcessEntry(
                                    null,
                                    entry,
                                    writer,
                                    preparator,
                                    solution,
                                    documentBefore,
                                    lifetime,
                                    settingsStore,
                                    contextBoundSettingsStoreLive,
                                    documentAfter,
                                    ecService,
                                    settingsToEntry,
                                    excludedEntries,
                                    language,
                                    map,
                                    fileName,
                                    null);
                            }
                            //writer.WriteEndElement();
                            //writer.WriteEndDocument();
                        }
                    }
                }

                CreateGeneralizedPropertiesDoc(path, host, map, ecService);
                CreateIndex(path, host, map, ecService);
            });
        }

        private static void CreateGeneralizedPropertiesDoc(
            string path, IApplicationHost host, OneToListMultimap<string, PropertyDescription> map,
            IEditorConfigSchema schema)
        {
            EditorConfigTests.CreateGeneralizedPropertiesTopic(path,host,map, schema);
            return;
            
            using (var writer = StartDocument(path, host, GeneralizedPropsFileName,
                "Generalized EditorConfig properties"))
            {
                foreach (var propInfo in schema.GetAllProperties())
                {
                    if (!propInfo.IsGeneralized || propInfo.Grandparent != null || propInfo.HasResharperPrefix)
                        continue;
                    Assertion.Assert(propInfo.Description != null && propInfo.Alias != null,
                        "propInfo.Description != null && propInfo.Alias != null {0}", propInfo.Alias);
                    writer.StartElem("chapter").Attr("id", propInfo.Alias).Attr("title", propInfo.Description);

                    var withResharperPrefix =
                        schema.GetSettingsForAlias(EditorConfigSchema.ReSharperPrefix + propInfo.Alias);
                    Assertion.Assert(withResharperPrefix != null,
                        "TODO: support generalized aliases w/o Resharper prefix, {0}", propInfo.Alias);
                    WriteAliases(
                        writer,
                        new[] {propInfo, withResharperPrefix},
                        "Property names:");

                    WriteAliases(
                        writer,
                        propInfo.Entries.SelectMany(schema.GetPropertiesForSettingsEntry).Distinct()
                            .Where(it => it.Grandparent == propInfo && it != propInfo && it != withResharperPrefix)
                            .ToArray(),
                        "Language-specific aliases:");

                    writer.StartElem("chapter").Attr("title", "Allows setting the following properties:");
                    //writer.StartElem("p").Str("Allows setting the following properties:").EndElem();
                    writer.StartElem("list");
                    foreach (var val1 in map[propInfo.Alias])
                    {
                        writer
                            .StartElem("li")
                            .StartElem("a")
                            .Attr("href", val1.FileName + ".xml")
                            .Attr("anchor", val1.Id)
                            .Str(val1.SectionDescription == null
                                ? val1.Description
                                : val1.SectionDescription + " - " + val1.Description)
                            .Str(" (" + val1.Language.PresentableName + ")")
                            .EndElem()
                            .EndElem();
                    }
                    writer.EndElem().EndElem();
                    ;

                    DescribePossibleValues(writer, propInfo.ValueType, propInfo.Values);

                    writer.EndElem();
                }

                //writer.EndElem().WriteEndDocument();
            }
        }

        private static XmlWriter StartDocument(
            string path,
            IApplicationHost host,
            string fileName,
            string title)
        {
            var writer = XmlWriter.Create(Path.Combine(path, fileName + ".xml"), XmlWriterEx.WriterSettings);
            writer.WriteStartDocument();
            writer.WriteStartElement("topic");
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xsi", "noNamespaceSchemaLocation", null,
                "http://helpserver.labs.intellij.net/help/topic.v2.xsd");
            writer.Attr("id", fileName);
            writer.Attr("title", title);

            writer.WriteComment("This topic was generated automatically by {0} built on {1}".FormatEx(
                host.HostProductInfo.PresentableInfoForAboutBox(),
                host.HostProductInfo.BuildDate.ToString("yyyy MMMM dd", DateTimeFormatInfo.InvariantInfo)));

            writer.StartElem("include").
                Attr("src", "FC.xml").Attr("include-id", "%thisTopic%").Attr("nullable","true").EndElem();
         

            return writer;
        }

        private static void CreateIndex(
            string path, IApplicationHost host, OneToListMultimap<string, PropertyDescription> map,
            IEditorConfigSchema ecService)
        {
            EditorConfigTests.CreateIndex(path, host, map, ecService);
            return;
            
            var fileName = "EditorConfig_Index";
            using (var writer = StartDocument(path, host, fileName, "Index of EditorConfig properties"))
            {
                writer
                    .StartElem("table")
                    .StartElem("tr")
                    .StartElem("td").Str("Property name").EndElem()
                    .StartElem("td").Str("Description").EndElem()
                    .EndElem();

                foreach (string propName in map.Keys.OrderBy())
                {
                    var values = map[propName];
                    writer
                        .StartElem("tr")
                        .StartElem("td").StartElem("code").Str(propName).EndElem().EndElem();
                    if (values.Count == 1)
                    {
                        var val = values.First();
                        writer
                            .StartElem("td")
                            .StartElem("a")
                            .Attr("href", val.FileName + ".xml")
                            .Attr("anchor", val.Id)
                            .Str(val.SectionDescription == null
                                ? val.Description
                                : val.SectionDescription + " - " + val.Description)
                            .Str(" (")
                            .Str(val.Language.PresentableName)
                            .Str(")")
                            .EndElem()
                            .EndElem();
                    }
                    else
                    {
                        var val = values.First();
                        var isGeneralized = values.Any(it => it.IsGeneralized);

                        writer.StartElem("td");
                        if (isGeneralized)
                        {
                            var propInfo = ecService.GetSettingsForAlias(propName);
                            Assertion.Assert(propInfo != null, "Property must exist {0}", propName);
                            if (propInfo.Grandparent != null)
                            {
                                propInfo = propInfo.Grandparent;
                            }
                            Assertion.Assert(!propInfo.Description.IsNullOrEmpty(),
                                "Description must be not null for property {0}",
                                propName);
                            writer
                                .StartElem("td")
                                .StartElem("a")
                                .Attr("href", GeneralizedPropsFileName + ".xml")
                                .Attr("anchor", propInfo.Alias)
                                .Str(propInfo.Description)
                                .Str(" (generalized)")
                                .EndElem()
                                .EndElem();
                        }
                        else
                        {
                            writer
                                .Str(val.SectionDescription == null
                                    ? val.Description
                                    : val.SectionDescription + " - " + val.Description)
                                .Str(", available for: ");
                            bool comma = false;
                            foreach (var val1 in values)
                            {
                                if (comma) writer.Str(", ");
                                comma = true;
                                writer
                                    .StartElem("a")
                                    .Attr("href", val1.FileName + ".xml")
                                    .Attr("anchor", val1.Id)
                                    .Str(val1.Language.PresentableName)
                                    .EndElem();
                            }
                        }

                        writer.EndElem();
                    }
                    writer.EndElem();
                }
                writer.EndElem(); //.EndElem().WriteEndDocument();
            }
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
            {
                FillSettingsToEntryDictionary(child, schemaLanguage, settingsToEntry, ecService);
            }
        }

        private static bool CalculateIfEntryShouldBeExcluded(
            ICodeStyleEntry entry, KnownLanguage schemaLanguage,
            Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>> settingsToEntry,
            HashSet<ICodeStyleEntry> excludedEntries)
        {
            bool excludeEntry = true;
            var settingsEntry = entry.SettingsEntry;
            if (settingsEntry != null)
            {
                var pair = settingsToEntry.GetValueSafe(settingsEntry);
                if (pair.First == entry)
                {
                    excludeEntry = false;
                }
            }

            foreach (var child in entry.Children)
            {
                if (!CalculateIfEntryShouldBeExcluded(child, schemaLanguage, settingsToEntry, excludedEntries))
                {
                    excludeEntry = false;
                }
            }

            if (excludeEntry) excludedEntries.Add(entry);
            return excludeEntry;
        }

        private static string ConvertToId(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            int nToRemove = 0;

            for (int a = 0; a < text.Length; a++)
            {
                char ch = text[a];
                if (!ch.IsIdentifierPart()) nToRemove++;
            }
            // Original?
            if (nToRemove == 0)
                return text;

            var sb = new StringBuilder(text.Length);
            bool afterUnderscore = false;
            for (int a = 0; a < text.Length; a++)
            {
                char ch = text[a];
                if (ch.IsIdentifierPart())
                {
                    if (ch == '_')
                    {
                        if (afterUnderscore) continue;
                        afterUnderscore = true;
                    }
                    else
                    {
                        afterUnderscore = false;
                    }
                    sb.Append(ch);
                }
                else
                {
                    if (afterUnderscore) continue;
                    sb.Append('_');
                    afterUnderscore = true;
                }
            }

            return sb.ToString();
        }

        private static void ProcessEntry(string parentId, ICodeStyleEntry entry, XmlWriter writer,
                                         CodePreviewPreparator preparator, ISolution solution, IDocument documentBefore,
                                         Lifetime lifetime,
                                         SettingsStore settingsStore,
                                         IContextBoundSettingsStoreLive contextBoundSettingsStoreLive,
                                         IDocument documentAfter, IEditorConfigSchema ecService,
                                         Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>>
                                             settingsToEntry,
                                         HashSet<ICodeStyleEntry> excludedEntries, KnownLanguage language,
                                         OneToListMultimap<string, PropertyDescription> map,
                                         string fileName,
                                         string parentDescription)
        {
            if (excludedEntries.Contains(entry)) return;

            string settingsName = null;
            var settingsEntry = entry.SettingsEntry;
            if (settingsEntry != null)
            {
                if (settingsToEntry.GetValueSafe(settingsEntry).First != entry)
                {
                    settingsEntry = null;
                }
            }

            IEditorConfigPropertyInfo propertyInfo = null;

            var description = entry.Description;
            if (settingsEntry != null)
            {
                propertyInfo = ecService.GetPropertiesForSettingsEntry(settingsEntry)
                    .OrderByDescending(it => it.Priority)
                    .FirstOrDefault();

                if (propertyInfo != null)
                {
                    settingsName = propertyInfo.Alias;
                    description = propertyInfo.Description;
                }
            }

            var id = settingsName
                     ?? (parentId == null
                         ? ConvertToId(description)
                         : parentId + "_" + ConvertToId(description));

            writer.StartElem("chapter").Attr("id", id).Attr("title", description);

            if (settingsEntry != null)
            {
                foreach (var prop in ecService.GetPropertiesForSettingsEntry(settingsEntry))
                {
                    map.Add(prop.Alias,
                        new PropertyDescription
                        {
                            Description = description,
                            FileName = fileName,
                            Id = id,
                            Language = language,
                            SectionDescription = parentDescription,
                            IsGeneralized = prop.IsGeneralized
                        });
                }

                WriteScalarEntry(
                    id, propertyInfo, entry, writer, preparator, solution, documentBefore, lifetime, settingsStore,
                    contextBoundSettingsStoreLive, documentAfter, settingsEntry, ecService, language);
            }

            foreach (var child in entry.Children)
            {
                ProcessEntry(id, child, writer, preparator, solution, documentBefore, lifetime, settingsStore,
                    contextBoundSettingsStoreLive, documentAfter, ecService, settingsToEntry, excludedEntries, language,
                    map,
                    fileName, description);
            }

            writer.EndElem();
        }

        private static void WriteScalarEntry(
            string id, IEditorConfigPropertyInfo propertyInfo,
            ICodeStyleEntry entry, XmlWriter writer,
            CodePreviewPreparator preparator,
            ISolution solution, IDocument documentBefore, Lifetime lifetime, SettingsStore settingsStore,
            IContextBoundSettingsStoreLive contextBoundSettingsStoreLive, IDocument documentAfter,
            SettingsScalarEntry settingsEntry, IEditorConfigSchema ecService,
            KnownLanguage language)
        {
            var aliases = ecService.GetPropertiesForSettingsEntry(settingsEntry)
                .OrderByDescending(it => it.Priority)
                .ToArray();

            if (aliases.Length > 0)
            {
                WriteAliases(writer, aliases, "Property names:");
            }

            var havePreview = entry as IHavePreview;
            var previewData = havePreview == null ? null : havePreview.PreviewData as CodeFormatterPreview;

            var previewType = previewData == null ? PreviewType.None : previewData.Type;
            if (previewType == PreviewType.Description)
            {
                writer.StartElem("code").Attr("style", "block").Str(previewData.Text).EndElem();
            }

            var valueType = settingsEntry.ValueClrType;
            var enumValues = propertyInfo.Values;
            var possibleValues = DescribePossibleValues(writer, valueType, enumValues);

            if (possibleValues != null && (previewType == PreviewType.Diff || previewType == PreviewType.Code))
            {
                writer.StartElem("chapter").Attr("title", "Examples:");
                Lifetimes.Using(lifetime, lf =>
                {
                    var previewSettings = settingsStore
                        .CreateNestedTransaction(lf, "Code formatter options UpdatePreview")
                        .BindToMountPoints(contextBoundSettingsStoreLive.InvolvedMountPoints);

                    if (previewData != null)
                    {
                        previewData.FixupSettingsForPreview(previewSettings);
                    }

                    string codeBefore = null;

                    if (previewType == PreviewType.Diff)
                    {
                        preparator.PrepareText(
                            solution,
                            documentBefore,
                            previewData.Text,
                            previewData.Parse,
                            null);

                        codeBefore = documentBefore.GetText();
                    }

                    writer.StartElem("table").Attr("header-style", "none");

                    var oldValue = previewSettings.GetValue(settingsEntry, null);

                    try
                    {
                        foreach (var pair in possibleValues)
                        {
                            if (previewType == PreviewType.Diff)
                            {
                                writer
                                    .StartElem("tr")
                                    .StartElem("td").StartElem("b").Str("Before formatting").EndElem().EndElem()
                                    .StartElem("td").StartElem("b").Str("After formatting, ").Str(pair.Item1).EndElem()
                                    .EndElem()
                                    .EndElem() // tr
                                    .StartElem("tr")
                                    .StartElem("td")
                                    .StartElem("code").Attr("style", "block").Attr("lang", language.PresentableName)
                                    .Attr("show-white-spaces", "true")
                                    .Str(codeBefore)
                                    .EndElem() // code
                                    .EndElem() // td
                                    .StartElem("td");
                            }
                            else
                            {
                                writer
                                    .StartElem("tr")
                                    .StartElem("td").StartElem("b").Str(pair.Item1).EndElem().EndElem()
                                    .EndElem()
                                    .StartElem("tr")
                                    .StartElem("td");
                            }

                            previewSettings.SetValue(settingsEntry, pair.Item3, null);
                            preparator.PrepareText(
                                solution,
                                documentAfter,
                                previewData.Text,
                                previewData.Parse,
                                previewSettings);

                            writer
                                .StartElem("code").Attr("style", "block").Attr("lang", language.PresentableName)
                                .Attr("show-white-spaces", "true")
                                .Str(documentAfter.GetText())
                                .EndElem() // code
                                .EndElem() // td
                                .EndElem(); // tr
                        }
                    }
                    finally
                    {
                        previewSettings.SetValue(settingsEntry, oldValue, null);
                    }

                    writer.EndElem(); // table
                });
                writer.EndElem();
            }
        }

        private static void WriteAliases(XmlWriter writer, IEditorConfigPropertyInfo[] aliases, string title)
        {
            var list = aliases.Select(it => it.Alias).ToList();
            foreach (var alias in aliases)
            {
                if (!alias.HasResharperPrefix)
                {
                    string element = EditorConfigSchema.ReSharperPrefix + alias.Alias;
                    if (list.Contains(element))
                    {
                        list.Remove(alias.Alias);
                        list.Remove(element);
                        list.Add("[{0}]{1}".FormatEx(EditorConfigSchema.ReSharperPrefix, alias.Alias));
                    }
                }
            }
            writer.StartElem("chapter").Attr("title", title).StartElem("p");
            bool addComma = false;
            foreach (var alias in list)
            {
                if (addComma)
                {
                    writer.WriteString(", ");
                }
                addComma = true;
                writer.WriteElementString("code", alias);
            }
            writer.WriteWhitespace(Environment.NewLine);
            writer.EndElem().EndElem();
        }

        private static Tuple<string, string, object>[] DescribePossibleValues(
            XmlWriter writer, PartCatalogType valueType, IReadOnlyCollection<IEditorConfigValueInfo> enumValues)
        {
            Tuple<string, string, object>[] possibleValues = null;
            var type = valueType.Bind();
            if (type == typeof(bool))
            {
                possibleValues = new[]
                {
                    new Tuple<string, string, object>("true", null, true),
                    new Tuple<string, string, object>("false", null, false)
                };
            }
            else if (type.IsEnum)
            {
                possibleValues =
                    enumValues
                        .Select(it =>
                            new Tuple<string, string, object>(it.Alias, it.Description, Enum.Parse(type, it.Value)))
                        .ToArray();
            }
            else if (type == typeof(int))
            {
                possibleValues = new[]
                {
                    new Tuple<string, string, object>("value: 0", null, 0),
                    new Tuple<string, string, object>("value: 1", null, 1),
                    new Tuple<string, string, object>("value: 2", null, 2)
                };
            }

            if (possibleValues != null)
            {
                writer.StartElem("chapter").Attr("title", "Possible values:");
                if (type == typeof(int))
                {
                    writer.WriteElementString("p", "an integer");
                }
                else if (type == typeof(bool))
                {
                    writer.WriteElementString("p", "true | false");
                }
                else
                {
                    writer.StartElem("list");
                    foreach (var value in possibleValues)
                    {
                        writer.StartElem("li").StartElem("code").Str(value.Item1).EndElem()
                            .Str(value.Item2 == null ? null : ": " + value.Item2).EndElem();
                    }
                    writer.EndElem();
                }
                writer.EndElem();
            }
            return possibleValues;
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