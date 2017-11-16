using System.Xml.Linq;
using JetBrains.Annotations;

namespace RsDocGenerator
{
  internal static class XmlHelpers
  {
    public static XDocument CreateHmTopic(string topicId, string title)
    {
      var topicDocument = new XDocument();
      //topicDocument.Add(new XComment("suppress AttributeValueVerifier"));
      topicDocument.Add(new XElement("topic"));

      XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
      topicDocument.Root.Add(
        new XAttribute(xsiNs + "noNamespaceSchemaLocation",
          "http://helpserver.labs.intellij.net/help/topic.v2.xsd"),
        new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
        new XAttribute("id", topicId),
        new XAttribute("title", title));

      AddAutoGenComment(topicDocument.Root);
      return topicDocument;
    }

    public static XElement CreateTwoColumnTable(string firstColName, string secondColName, string firstColWidth)
    {
      return CreateTable(new[] {firstColName, secondColName}, new[] {firstColWidth});
    }

    public static XElement CreateTable([NotNull] string[] colNames, [CanBeNull] string[] colWidths)
    {
      var table = new XElement("table");
      var headerRow = new XElement("tr");
      for (int index = 0; index < colNames.Length; index++)
      {
        var headerCell = new XElement("td", colNames[index]);
        if (colWidths != null && colWidths.Length > index)
          headerCell.Add(new XAttribute("width", colWidths[index]));
        headerRow.Add(headerCell);
      }
      table.Add(headerRow);
      return table;
    }

    public static XElement CreateChunk(string includeiD)
    {
      return new XElement("chunk", new XAttribute("include-id", includeiD));
    }

    public static XElement CreateInclude(string src, string id, bool nullable)
    {
      return new XElement("include",
        new XAttribute("nullable", nullable ? "true" : "false"),
        new XAttribute("src", src + ".xml"),
        new XAttribute("include-id", id.NormalizeStringForAttribute()));
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
      return CreateChapter(title, id, true);
    }

    private static XElement CreateChapter(string title, string id, bool needId)
    {
      var chapter = new XElement("chapter",
        new XAttribute("caps", "aswritten"),
        new XAttribute("title", title));
      if (needId)
        chapter.Add(new XAttribute("id", id));
      
      return chapter;
    }

    public static XElement CreateHyperlink([CanBeNull] string content, [CanBeNull] string href, [CanBeNull] string anchor, bool nullable)
    {
      var link = new XElement("a", content);
      if(nullable)
        link.Add(new XAttribute("nullable", "true"));
      if (href != null)
        link.Add(new XAttribute("href", href.Contains("http") ? href : href + ".xml"));
      if (anchor != null)
        link.Add(new XAttribute("anchor", anchor.NormalizeStringForAttribute()));
      return link;
    }

    public static XElement CreateCodeBlock([NotNull] string content, [CanBeNull] string lang)
    {
      var codeElement = new XElement("code", content.CleanCodeSample(),
        new XAttribute("style", "block"));

      if (lang == null || lang == "Global")
        codeElement.Add(new XAttribute("highlight", "none"));
      else
      {
        switch (lang)
        {
          case "ASP.NET (C#)":
          case "ASP.NET(C#)":
          case "ASP.NET (VB)":
          case "ASP.NET(VB.NET)":
            lang = "ASP.NET";
            break;
          case "XAML (C#)":
          case "XAML (VB)":
          case "HTML-Like":
          case "Razor (C#)":
          case "Angular 2 HTML":
            lang = "HTML";
            break;
          case "Resx":
            lang = "XML";
            break;
        }
        codeElement.Add(new XAttribute("lang", lang));
      }
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