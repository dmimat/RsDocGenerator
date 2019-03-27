using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.Application.DataContext;
using JetBrains.Application.Settings;
using JetBrains.Application.Settings.Implementation;
using JetBrains.DataFlow;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle;
using JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle.ViewModels;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.EditorConfig;
using JetBrains.ReSharper.Psi.impl.EditorConfig;
using JetBrains.Util;
using JetBrains.Util.dataStructures;

namespace RsDocGenerator
{
    public static class EditorConfigXdoc
    {
        private const string GeneralizedPropsFileName = "EditorConfig_Generalized";

        public static void CreateIndex(string path, IDataContext context,
            OneToListMultimap<string, RsDocExportEditorConfigStyles.PropertyDescription> map,
            IEditorConfigSchema ecService)
        {
            const string editorConfigIndexTopicId = "EditorConfig_Index";
            var editorConfigIndexTopic =
                XmlHelpers.CreateHmTopic(editorConfigIndexTopicId, "Index of EditorConfig properties");

            editorConfigIndexTopic.Root.Add(XmlHelpers.CreateInclude("FC", "%thisTopic%"));

            var table = XmlHelpers.CreateTwoColumnTable("Property name", "Description", "50%");

            var tableRows = new SortedDictionary<string, XElement>();

            foreach (string propName in map.Keys.OrderBy())
            {
                var propRow = new XElement("tr");
                propRow.Add(new XElement("td", new XElement("code", propName)));

                var values = map[propName];

                if (values.Count == 1)
                {
                    var val = values.First();
                    var lang = val.Language.PresentableName;

                    if (!lang.IsLangSupportedInRider())
                        propRow.Add(new XAttribute("product", "!rdr"));

                    var content = (val.SectionDescription == null
                                      ? val.Description
                                      : val.SectionDescription + " - " + val.Description) + " (" + lang + ")";
                    propRow.Add(new XElement("td",
                        XmlHelpers.CreateHyperlink(content, val.FileName, val.Id)));
                }
                else
                {
                    var isGeneralized = values.Any(it => it.IsGeneralized);
                    var contentTd = new XElement("td");
                    if (isGeneralized)
                    {
                        var propInfo = ecService.GetSettingsForAlias(propName);
                        Assertion.Assert(propInfo != null, "Property must exist {0}", propName);
                        if (propInfo.Grandparent != null)
                            propInfo = propInfo.Grandparent;

                        Assertion.Assert(!propInfo.Description.IsNullOrEmpty(),
                            "Description must be not null for property {0}",
                            propName);
                        contentTd.Add(XmlHelpers.CreateHyperlink(propInfo.Description + " (generalized)",
                            GeneralizedPropsFileName, propInfo.Alias, false));
                    }
                    else
                    {
                        var val = values.First();
                        contentTd.Add(val.SectionDescription == null
                            ? val.Description
                            : val.SectionDescription + " - " + val.Description + ", available for: ");
                        bool comma = false;
                        foreach (var val1 in values)
                        {
                            var currentElement = contentTd;
                            var lang = val1.Language.PresentableName;
                            if (!lang.IsLangSupportedInRider())
                            {
                                currentElement = new XElement("for", new XAttribute("product", "!rdr"));
                                contentTd.Add(currentElement);
                            }

                            if (comma) currentElement.Add(", ");
                            comma = true;
                            var link = XmlHelpers.CreateHyperlink(lang, val1.FileName, val1.Id, false);
                            currentElement.Add(link);
                        }
                    }

                    propRow.Add(contentTd);
                }

                if (!tableRows.ContainsKey(propName))
                    tableRows.Add(propName, propRow);
            }

            var featureDigger = new FeatureDigger(context);
            var configurableInspetions = featureDigger.GetConfigurableInspections();
            foreach (var inspection in configurableInspetions.Features)
            {
                var propName = inspection.EditorConfigId;
                var propRow = new XElement("tr");
                if (!inspection.Lang.IsLangSupportedInRider())
                    propRow.Add(new XAttribute("product", "!rdr"));
                propRow.Add(new XElement("td", new XElement("code", propName)));
                propRow.Add(new XElement("td",
                    XmlHelpers.CreateHyperlink("Code Inspection", "Code_Analysis__Code_Inspections"),
                    new XText(": "),
                    XmlHelpers.CreateHyperlink(inspection.Text,
                        CodeInspectionHelpers.TryGetStaticHref(inspection.Id), null, true)));
                if (!tableRows.ContainsKey(propName))
                    tableRows.Add(inspection.EditorConfigId, propRow);
            }

            foreach (var row in tableRows)
                table.Add(row.Value);
            editorConfigIndexTopic.Root.Add(table);
            editorConfigIndexTopic.Save(Path.Combine(path, editorConfigIndexTopicId + ".xml"));
        }

        public static void CreateGeneralizedPropertiesTopic(string path, IApplicationHost host,
            OneToListMultimap<string, RsDocExportEditorConfigStyles.
                PropertyDescription> map,
            IEditorConfigSchema schema)
        {
            var editorConfigGeneralizedTopic =
                XmlHelpers.CreateHmTopic(GeneralizedPropsFileName, "Generalized EditorConfig properties");
            editorConfigGeneralizedTopic.Root.Add(XmlHelpers.CreateInclude("FC", "%thisTopic%"));

            foreach (var propInfo in schema.GetAllProperties())
            {
                if (!propInfo.IsGeneralized || propInfo.Grandparent != null || propInfo.HasResharperPrefix)
                    continue;
                Assertion.Assert(propInfo.Description != null && propInfo.Alias != null,
                    "propInfo.Description != null && propInfo.Alias != null {0}", propInfo.Alias);

                var chapterTopLevel = XmlHelpers.CreateChapter(propInfo.Description, propInfo.Alias);

                var withResharperPrefix =
                    schema.GetSettingsForAlias(EditorConfigSchema.ReSharperPrefix + propInfo.Alias);
                Assertion.Assert(withResharperPrefix != null,
                    "TODO: support generalized aliases w/o Resharper prefix, {0}", propInfo.Alias);

                WriteAliases(chapterTopLevel, new[] {propInfo, withResharperPrefix}, "Property names:");

                WriteAliases(chapterTopLevel,
                    propInfo.Entries.SelectMany(schema.GetPropertiesForSettingsEntry).Distinct()
                        .Where(it => it.Grandparent == propInfo && it != propInfo && it != withResharperPrefix)
                        .ToArray(),
                    "Language-specific aliases:");

                var chapterAllows = XmlHelpers.CreateChapterWithoutId("Allows setting the following properties:");
                var list = new XElement("list");

                foreach (var val1 in map[propInfo.Alias])
                {
                    var lang = val1.Language.PresentableName;
                    var content = val1.SectionDescription == null
                        ? val1.Description
                        : val1.SectionDescription + " - " + val1.Description + " (" + lang + ")";
                    var li = new XElement("li", XmlHelpers.CreateHyperlink(content, val1.FileName, val1.Id, false));
                    if (!lang.IsLangSupportedInRider())
                        li.Add(new XAttribute("product", "!rdr"));
                    list.Add(li);
                }

                chapterAllows.Add(list);
                chapterTopLevel.Add(chapterAllows);

                DescribePossibleValues(chapterTopLevel, propInfo.ValueTypeInfo.ValueType,
                    propInfo.ValueTypeInfo.Values);

                editorConfigGeneralizedTopic.Root.Add(chapterTopLevel);
            }

            editorConfigGeneralizedTopic.Save(Path.Combine(path, GeneralizedPropsFileName + ".xml"));
        }

        public static void ProcessEntries(KnownLanguage language, ICodeStylePageSchema schema, string path,
            IDocument documentBefore, IDocument documentAfter, Lifetime lifetime,
            SettingsStore settingsStore, IContextBoundSettingsStoreLive contextBoundSettingsStoreLive,
            IEditorConfigSchema ecService,
            Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>> settingsToEntry,
            HashSet<ICodeStyleEntry> excludedEntries, CodePreviewPreparator preparator, ISolution solution,
            OneToListMultimap<string, RsDocExportEditorConfigStyles.PropertyDescription> map)
        {
            var topicId = "EditorConfig_" + language.Name.Replace(" ", "_") + "_" + schema.GetType().Name;
            var topic = XmlHelpers.CreateHmTopic(topicId,
                "{0} - {1}".FormatEx(language.PresentableName, schema.PageName));
            topic.Root.Add(XmlHelpers.CreateInclude("FC", "%thisTopic%", true));

            foreach (var entry in schema.Entries)
            {
                ProcessEntry(
                    null,
                    entry,
                    topic.Root,
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
                    null, topicId);
            }

            topic.Save(Path.Combine(path, topicId + ".xml"));
        }

        private static void ProcessEntry(string parentId, ICodeStyleEntry entry, XElement container,
            CodePreviewPreparator preparator, ISolution solution, IDocument documentBefore,
            Lifetime lifetime,
            SettingsStore settingsStore,
            IContextBoundSettingsStoreLive contextBoundSettingsStoreLive,
            IDocument documentAfter, IEditorConfigSchema ecService,
            Dictionary<SettingsEntry, Pair<ICodeStyleEntry, KnownLanguage>>
                settingsToEntry,
            HashSet<ICodeStyleEntry> excludedEntries, KnownLanguage language,
            OneToListMultimap<string, RsDocExportEditorConfigStyles.PropertyDescription> map,
            string parentDescription, string fileName)
        {
            if (excludedEntries.Contains(entry)) return;

            string settingsName = null;
            var settingsEntry = entry.SettingsEntry;
            if (settingsEntry != null)
                if (settingsToEntry.GetValueSafe(settingsEntry).First != entry)
                    settingsEntry = null;

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

            var chapter = XmlHelpers.CreateChapter(description, id);

            if (settingsEntry != null)
            {
                foreach (var prop in ecService.GetPropertiesForSettingsEntry(settingsEntry))
                {
                    map.Add(prop.Alias,
                        new RsDocExportEditorConfigStyles.PropertyDescription
                        {
                            Description = description,
                            FileName = fileName,
                            Id = id,
                            Language = language,
                            SectionDescription = parentDescription,
                            IsGeneralized = prop.IsGeneralized
                        });
                }

                WriteScalarEntry(propertyInfo, entry, chapter, preparator, solution, documentBefore, lifetime,
                    settingsStore,
                    contextBoundSettingsStoreLive, documentAfter, settingsEntry, ecService, language);
            }

            foreach (var child in entry.Children)
                ProcessEntry(id, child, chapter, preparator, solution, documentBefore, lifetime, settingsStore,
                    contextBoundSettingsStoreLive, documentAfter, ecService, settingsToEntry, excludedEntries, language,
                    map, description, fileName);

            container.Add(chapter);
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
                        afterUnderscore = false;

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

        private static void WriteScalarEntry(
            IEditorConfigPropertyInfo propertyInfo,
            ICodeStyleEntry entry, XElement container,
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
                WriteAliases(container, aliases, "Property names:");

            var havePreview = entry as IHavePreview;
            var previewData = havePreview == null ? null : havePreview.PreviewData as CodeFormatterPreview;

            var previewType = previewData == null ? PreviewType.None : previewData.Type;
            if (previewType == PreviewType.Description)
                container.Add(XmlHelpers.CreateCodeBlock(previewData.Text, language.Name, true));

            var valueType = settingsEntry.ValueClrType;
            var enumValues = propertyInfo.ValueTypeInfo.Values;
            var possibleValues = DescribePossibleValues(container, valueType, enumValues);

            if (possibleValues != null && (previewType == PreviewType.Diff || previewType == PreviewType.Code))
            {
                var chapterExamples = XmlHelpers.CreateChapterWithoutId("Examples:");

                Lifetimes.Using(lifetime, lf =>
                {
                    var previewSettings = settingsStore
                        .CreateNestedTransaction(lf, "Code formatter options UpdatePreview")
                        .BindToMountPoints(contextBoundSettingsStoreLive.InvolvedMountPoints);

                    if (previewData != null)
                        previewData.FixupSettingsForPreview(previewSettings);

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

                    var oldValue = previewSettings.GetValue(settingsEntry, null);

                    try
                    {
                        foreach (var pair in possibleValues)
                        {
                            XElement table;
                            var tr = new XElement("tr");

                            if (previewType == PreviewType.Diff)
                            {
                                table = XmlHelpers.CreateTwoColumnTable("Before formatting",
                                    "After formatting, " + pair.Item1, "50%");
                                tr.Add(new XElement("td",
                                    XmlHelpers.CreateCodeBlock(codeBefore, language.PresentableName, true)));
                            }
                            else
                                table = XmlHelpers.CreateTable(new[] {pair.Item1}, null);

                            previewSettings.SetValue(settingsEntry, pair.Item3, null);
                            preparator.PrepareText(
                                solution,
                                documentAfter,
                                previewData.Text,
                                previewData.Parse,
                                previewSettings);

                            tr.Add(new XElement("td",
                                XmlHelpers.CreateCodeBlock(documentAfter.GetText(), language.PresentableName, true)));
                            table.Add(tr);
                            chapterExamples.Add(table);
                        }
                    }
                    finally
                    {
                        previewSettings.SetValue(settingsEntry, oldValue, null);
                    }

                    container.Add(chapterExamples);
                });
            }
        }

        private static void WriteAliases(XElement container, IEditorConfigPropertyInfo[] aliases, string title)
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

            var chapter = XmlHelpers.CreateChapterWithoutId(title);
            var paragraph = new XElement("p");

            bool addComma = false;
            foreach (var alias in list)
            {
                if (addComma)
                    paragraph.Add(", ");
                addComma = true;
                paragraph.Add(new XElement("code", alias));
            }

            chapter.Add(paragraph);
            container.Add(chapter);
        }

        private static Tuple<string, string, object>[] DescribePossibleValues(
            XElement container, PartCatalogType valueType, IReadOnlyCollection<IEditorConfigValueInfo> enumValues)
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
                var chapterPossibleValues = XmlHelpers.CreateChapterWithoutId("Possible values:");

                if (type == typeof(int))
                    chapterPossibleValues.Add(new XElement("p", "an integer"));
                else if (type == typeof(bool))
                    chapterPossibleValues.Add(new XElement("p", new XElement("code", "true | false")));
                else
                {
                    var list = new XElement("list");
                    foreach (var value in possibleValues)
                    {
                        list.Add(new XElement("li",
                            new XElement("code", value.Item1), value.Item2 == null ? null : ": " + value.Item2));
                    }

                    chapterPossibleValues.Add(list);
                }

                container.Add(chapterPossibleValues);
            }

            return possibleValues;
        }
    }
}