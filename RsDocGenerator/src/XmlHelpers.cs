﻿using System.Xml.Linq;
using JetBrains.Annotations;

namespace RsDocGenerator
{
    internal static class XmlHelpers
    {
        public static XDocument CreateHmTopic(string topicId, string title)
        {
            var topicDocument = new XDocument(
                new XDocumentType("topic", null, "https://resources.jetbrains.com/stardust/html-entities.dtd", null));
            //topicDocument.Add(new XComment("suppress AttributeValueVerifier"));
            topicDocument.Add(new XElement("topic"));

            XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
            topicDocument.Root.Add(
                new XAttribute(xsiNs + "noNamespaceSchemaLocation",
                    "https://resources.jetbrains.com/stardust/topic.v2.xsd"),
                new XAttribute(XNamespace.Xmlns + "xsi", xsiNs),
                new XAttribute("id", topicId),
                new XAttribute("title", title));

            AddAutoGenComment(topicDocument.Root);
            topicDocument.Root.Add(CreateInclude("GEN", topicId + "_start", true));
            return topicDocument;
        }

        public static XElement CreateTwoColumnTable(string firstColName, string secondColName, string firstColWidth)
        {
            return CreateTable(new[] {firstColName, secondColName}, new[] {firstColWidth});
        }

        public static XElement CreateTable(
            [NotNull] string[] colNames, 
            [CanBeNull] string[] colWidths = null,
            [CanBeNull] string id = null)
        {
            var table = new XElement("table");
            if (id != null) 
                table.Add(new XAttribute("id", id));
            var headerRow = new XElement("tr");
            for (var index = 0; index < colNames.Length; index++)
            {
                var headerCell = new XElement("td", colNames[index]);
                if (colWidths != null && colWidths.Length > index)
                    headerCell.Add(new XAttribute("width", colWidths[index]));
                headerRow.Add(headerCell);
            }

            table.Add(headerRow);
            return table;
        }

        public static XElement CreateChunk(string includeId)
        {
            return new XElement("chunk", new XAttribute("id", includeId.NormalizeStringForAttribute()));
        }

        public static XElement CreateInclude(string src, string id, bool nullable = false)
        {
            return new XElement("include",
                new XAttribute("nullable", nullable ? "true" : "false"),
                new XAttribute("src", src + ".xml"),
                new XAttribute("include-id", id.NormalizeStringForAttribute()));
        }
        
        public static XElement CreateVariable(string name, string value)
        {
            return new XElement("var",
                new XAttribute("name", name),
                new XAttribute("value", value));
        }

        public static XElement CreateChapter(string title)
        {
            return CreateChapter(title, title.NormalizeStringForAttribute());
        }

        public static XElement CreateChapterWithoutId(string title)
        {
            return CreateChapter(title, null, false);
        }

        public static XElement CreateChapter(string title, string id)
        {
            return CreateChapter(title, id.NormalizeStringForAttribute(), true);
        }

        private static XElement CreateChapter(string title, string id, bool needId)
        {
            if (title == null)
                title = "UNKNOWN";
            var chapter = new XElement("chapter",
                new XAttribute("caps", "aswritten"),
                new XAttribute("title", title));
            if (!needId) return chapter;
            if (id == null)
                id = "UNKNOWN";
            chapter.Add(new XAttribute("id", id));
            return chapter;
        }

        public static XElement CreateHyperlink([CanBeNull] string content, [CanBeNull] string href,
            [CanBeNull] string anchor = null, bool nullable = false)
        {
            var link = new XElement("a", content);
            if (nullable)
                link.Add(new XAttribute("nullable", "true"));
            if (href != null)
                link.Add(new XAttribute("href", href.Contains("http") ? href : href + ".xml"));
            if (anchor != null)
                link.Add(new XAttribute("anchor", anchor.NormalizeStringForAttribute()));
            return link;
        }

        public static XElement CreateCodeBlock([NotNull] string content, [CanBeNull] string lang, bool showSpaces)
        {
            var codeElement = new XElement("code", content.CleanCodeSample(),
                new XAttribute("style", "block"),
                new XAttribute("interpolate-variables", "false")
                );

            if (lang == null || lang == "Global" || lang == "Protobuf")
            {
                codeElement.Add(new XAttribute("highlight", "none"));
            }
            else
            {
                switch (lang)
                {
                    case "HTML-Like":
                    case "Razor (C#)":
                    case "Razor CSharp":
                    case "Angular 2 HTML":
                        lang = "HTML";
                        break;
                    case "CPP":
                    case "Unreal Engine":
                        lang = "C++";
                        break;
                    case "JAVA_SCRIPT":
                        lang = "JavaScript";
                        break;
                    case "VBASIC":
                        lang = "VB.NET";
                        break;
                    case "Resx":
                    case "ASP.NET":
                    case "ASP.NET (C#)":
                    case "ASP.NET(C#)":
                    case "ASP.NET (VB)":
                    case "ASP.NET(VB.NET)":
                    case "XAML":
                    case "XAML (C#)":
                    case "XAML (VB)":
                        lang = "XML";
                        break;
                    case "Unity":
                    case "XMLDOC":
                    case "Test":
                    case "ShaderLab":
                    case "SHADERLAB":
                        lang = "C#";
                        break;
                }

                codeElement.Add(new XAttribute("lang", lang));
            }

            if (showSpaces)
                codeElement.Add(new XAttribute("show-white-spaces", "true"));

            return codeElement;
        }

        public static void AddAutoGenComment(XElement element)
        {
            element.Add(
                new XComment(
                    "This topic was generated automatically with the ReSharper Documentation Generator plugin. " +
                    "ReSharper version: " + GeneralHelpers.GetCurrentVersion()));
        }

        public static void AddRsOnlyAttribute(XElement element)
        {
            element.Add(new XAttribute("product", "rs"));
        }
    }
}