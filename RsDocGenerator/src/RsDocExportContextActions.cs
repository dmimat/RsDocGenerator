using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.UI.ActionsRevised;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
  [Action("RsDocExportContextActions", "Export Context Actions", Id = 8673421)]
  internal class RsDocExportContextActions : IExecutableAction
  {
    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return true;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      using (var brwsr = new FolderBrowserDialog() {Description = "Choose where to save XML topics."})
      {
        if (brwsr.ShowDialog() == DialogResult.Cancel) return;
        string saveDirectoryPath = brwsr.SelectedPath;
        
        const string caTopicId = "CA_Chunks";
        var caLibrary = XmlHelpers.CreateHmTopic(caTopicId);
        var tablesByLanguage = new Dictionary<string, XElement>();
        var sortedActions = context.GetComponent<IContextActionTable>().AllActions.OrderBy(ca => ca.Name);

        caLibrary.Root.Add(new XComment("Total context actions in ReSharper " + GeneralHelpers.GetCurrentVersion() + ": " + sortedActions.Count()));
        foreach (var ca in sortedActions)
        {
          var lang = ca.Group ?? "Unknown";

          if (!tablesByLanguage.ContainsKey(lang))
            tablesByLanguage.Add(lang, XmlHelpers.CreateTwoColumnTable("Name", "Description", "40%"));

          tablesByLanguage.GetValue(lang).Add(new XElement("tr",
            new XElement("td", new XElement("b", ca.Name)),
            new XElement("td",
              ca.Description ?? "",
              XmlHelpers.CreateInclude("CA_Static_Chunks", ca.MergeKey.NormalizeStringForAttribute()))));
        }

        foreach (var table in tablesByLanguage)
        {
          var languageChunk = XmlHelpers.CreateChunk("ca_" + table.Key.NormalizeStringForAttribute());
          string langText = table.Key == "Common" ? "common use" : table.Key;
          languageChunk.Add(new XElement("p", "ReSharper provides the following context actions for " + langText + ":"));
          languageChunk.Add(table.Value);
          caLibrary.Root.Add(languageChunk);
        }

        caLibrary.Save(Path.Combine(saveDirectoryPath, caTopicId + ".xml"));
        MessageBox.ShowInfo("Context Actions exported successfully");
      }
    }
  }
}