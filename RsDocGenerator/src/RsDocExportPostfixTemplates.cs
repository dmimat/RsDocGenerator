﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.PostfixTemplates;

namespace RsDocGenerator
{
    [Action("RsDocExportPostfixTemplates", "Export Postfix Templates", Id = 7569)]
    internal class RsDocExportPostfixTemplates : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            var allTemplates = context.GetComponent<PostfixTemplatesManager>().AllRegisteredPostfixTemplates.ToList();
            const string postfixTopicId = "Postfix_Templates_Generated";
            var postfixLibrary = XmlHelpers.CreateHmTopic(postfixTopicId, "Postfix templates chunks");
            postfixLibrary.Root.Add(new XComment("Total postifix templates in ReSharper " +
                                                 GeneralHelpers.GetCurrentVersion() + ": " + allTemplates.Count));

            var langs = allTemplates.Select(x => x.Template.Language).Distinct();
            foreach (var lang in langs)
            {
                var templateInLang = allTemplates.Where(x => x.Template.Language.Equals(lang))
                    .OrderBy(t => t.Annotation.TemplateName);
                AddLangChunk(postfixLibrary, templateInLang, lang.Name);
            }

            postfixLibrary.Save(Path.Combine(outputFolder + "\\CodeTemplates", postfixTopicId + ".xml"));
            return "Postfix templates";
        }

        private static void AddLangChunk(XDocument library, IEnumerable<PostfixTemplateMetadata> templates, string lang)
        {
            var postfixChunk = XmlHelpers.CreateChunk("postfix_table_" + lang);
            var macroTable = XmlHelpers.CreateTable(new[] {"Shortcut", "Description", "Example"}, null);
            foreach (var postTempalte in templates)
            {
                var postfixRow = new XElement("tr");
                var shortcut = postTempalte.Annotation.TemplateName;
                var description = postTempalte.Annotation.Description;
                var example = postTempalte.Annotation.Example;

                var shortcutCell = XElement.Parse("<td><b>." + shortcut + "</b></td>");
                shortcutCell.Add(new XAttribute("id", lang + "_" + shortcut));
                var descriptionCell = XElement.Parse("<td>" + description + "</td>");
                var exampleCell = new XElement("td", new XElement("code", example));

                postfixRow.Add(shortcutCell, descriptionCell, exampleCell);
                macroTable.Add(postfixRow);
            }

            postfixChunk.Add(macroTable);

            library.Root.Add(postfixChunk);
        }
    }
}