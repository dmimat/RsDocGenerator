using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.PostfixTemplates;
using JetBrains.UI.ActionsRevised;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
    [Action("RsDocExportPostfixTemplates", "Export Postfix Templates", Id = 7569)]
    internal class RsDocExportPostfixTemplates : IExecutableAction
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            using (var brwsr = new FolderBrowserDialog() {Description = "Choose where to save XML topics."})
            {
                const string postfixTopicId = "Postfix_Templates_Generated";
                if (brwsr.ShowDialog() == DialogResult.Cancel) return;
                string saveDirectoryPath = brwsr.SelectedPath;
                string fileName = Path.Combine(saveDirectoryPath, postfixTopicId + ".xml");

              var postfixLibrary = XmlHelpers.CreateHmTopic(postfixTopicId);
                var postfixChunk = XmlHelpers.CreateChunk("postfix_table");
                var macroTable = XmlHelpers.CreateTable(new[] {"Shortcut", "Description", "Example"}, null);

                var allSortedPostfix =
                    context.GetComponent<PostfixTemplatesManager>()
                        .AllRegisteredPostfixTemplates.OrderBy(template => template.Annotations.TemplateName);
                postfixLibrary.Root.Add(
                    new XComment("Total postifix templates in ReSharper " + 
                      GeneralHelpers.GetCurrentVersion() + ": " + allSortedPostfix.Count()));

                foreach (var postTempalte in allSortedPostfix)
                {
                    var postfixRow = new XElement("tr");
                    var shortcut = postTempalte.Annotations.TemplateName;
                    var description = postTempalte.Annotations.Description;
                    var example = postTempalte.Annotations.Example;
                    
                    var shortcutCell = XElement.Parse("<td><b>." + shortcut + "</b></td>");
                    shortcutCell.Add(new XAttribute("id", shortcut));
                    var descriptionCell = XElement.Parse("<td>" + description + "</td>");
                    var exampleCell = new XElement("td", new XElement("code", example));
  
                    postfixRow.Add(shortcutCell, descriptionCell, exampleCell);
                    macroTable.Add(postfixRow);
                }

                postfixChunk.Add(macroTable);
                
                postfixLibrary.Root.Add(postfixChunk);
                postfixLibrary.Save(fileName);
                MessageBox.ShowInfo("Postfix templates saved successfully");
            }
        }
    }
}
