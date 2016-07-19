using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.PostfixTemplates;
using JetBrains.UI.ActionsRevised;

namespace RsDocGenerator
{
  [Action("RsDocExportPostfixTemplates", "Export Postfix Templates", Id = 7569)]
  internal class RsDocExportPostfixTemplates : RsDocExportBase
  {
    protected override string GenerateContent(IDataContext context, string outputFolder)
    {
      const string postfixTopicId = "Postfix_Templates_Generated";
      var postfixLibrary = XmlHelpers.CreateHmTopic(postfixTopicId);
      var postfixChunk = XmlHelpers.CreateChunk("postfix_table");
      var macroTable = XmlHelpers.CreateTable(new[] {"Shortcut", "Description", "Example"}, null);

      var allSortedPostfix =
        context.GetComponent<PostfixTemplatesManager>()
          .AllRegisteredPostfixTemplates.OrderBy(template => template.Annotation.TemplateName);
      postfixLibrary.Root.Add(
        new XComment("Total postifix templates in ReSharper " +
                     GeneralHelpers.GetCurrentVersion() + ": " + allSortedPostfix.Count()));

      foreach (var postTempalte in allSortedPostfix)
      {
        var postfixRow = new XElement("tr");
        var shortcut = postTempalte.Annotation.TemplateName;
        var description = postTempalte.Annotation.Description;
        var example = postTempalte.Annotation.Example;

        var shortcutCell = XElement.Parse("<td><b>." + shortcut + "</b></td>");
        shortcutCell.Add(new XAttribute("id", shortcut));
        var descriptionCell = XElement.Parse("<td>" + description + "</td>");
        var exampleCell = new XElement("td", new XElement("code", example));

        postfixRow.Add(shortcutCell, descriptionCell, exampleCell);
        macroTable.Add(postfixRow);
      }

      postfixChunk.Add(macroTable);

      postfixLibrary.Root.Add(postfixChunk);
      postfixLibrary.Save(Path.Combine(outputFolder, postfixTopicId + ".xml"));
      return "Postfix templates";
    }
  }
}
