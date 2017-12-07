using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.ReSharper.Psi.EditorConfig;
using JetBrains.ReSharper.Psi.impl.EditorConfig;
using JetBrains.Util;
using JetBrains.Util.dataStructures;

namespace RsDocGenerator
{
    public static class EditorConfigXdoc
    {
        private const string GeneralizedPropsFileName = "EditorConfig_Generalized";

        public static void CreateIndex(string path, IApplicationHost host,
                                       OneToListMultimap<string, RsDocExportEditorConfigStyles.PropertyDescription> map,
                                       IEditorConfigSchema ecService)
        {
            const string editorConfigIndexTopicId = "EditorConfig_Index";
            var editorConfigIndexTopic =
                XmlHelpers.CreateHmTopic(editorConfigIndexTopicId, "Index of EditorConfig properties");
            var table = XmlHelpers.CreateTwoColumnTable("Property name", "Description", "40%");

            editorConfigIndexTopic.Root.Add(XmlHelpers.CreateInclude("FC", "%thisTopic%", false));

            foreach (string propName in map.Keys.OrderBy())
            {
                var propRow = new XElement("tr");
                propRow.Add(new XElement("td", new XElement("code", propName)));

                var values = map[propName];

                if (values.Count == 1)
                {
                    var val = values.First();
                    var lang = val.Language.PresentableName;

                    if (lang == "C++")
                        propRow.Add(new XAttribute("product", "!rdr"));

                    var content = (val.SectionDescription == null
                                      ? val.Description
                                      : val.SectionDescription + " - " + val.Description) + " (" + lang + ")";
                    propRow.Add(new XElement("td",
                        XmlHelpers.CreateHyperlink(content, val.FileName, val.Id, false)));
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
                        {
                            propInfo = propInfo.Grandparent;
                        }
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
                            if (lang == "C++")
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
                table.Add(propRow);
            }
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
            editorConfigGeneralizedTopic.Root.Add(XmlHelpers.CreateInclude("FC", "%thisTopic%", false));

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
                    if (lang == "C++")
                        li.Add(new XAttribute("product", "!rdr"));
                    list.Add(li);
                }
                chapterAllows.Add(list);
                chapterTopLevel.Add(chapterAllows);

                DescribePossibleValues(chapterTopLevel, propInfo.ValueType, propInfo.Values);

                editorConfigGeneralizedTopic.Root.Add(chapterTopLevel);
            }

            editorConfigGeneralizedTopic.Save(Path.Combine(path, GeneralizedPropsFileName + ".xml"));
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
                {
                    chapterPossibleValues.Add(new XElement("p", "an integer"));
                }
                else if (type == typeof(bool))
                {
                    chapterPossibleValues.Add(new XElement("p", new XElement("code", "true | false")));
                }
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