using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Bulk;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Feature.Services.QuickFixes;
using JetBrains.UI.ActionsRevised;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
  [Action("RsDocExportFixInScope", "Export items supporting Fix in Scope", Id = 4343)]
  internal class RsDocExportFixInScope : IExecutableAction
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

        const string caTopicId = "Fix_in_Scope_Chunks";
        var inScopeLibrary = XmlHelpers.CreateHmTopic(caTopicId);

        var qfChunk = XmlHelpers.CreateChunk("qf_list");
        var caChunk = XmlHelpers.CreateChunk("ca_list");

        var qfListByLang = new List<Tuple<string, XElement>>();
        var caListByLang = new List<Tuple<string, XElement>>();
        var qfLists = new Dictionary<string, XElement>();
        var caLists = new Dictionary<string, XElement>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
          Type[] types;
          try { types = assembly.GetTypes(); }
          catch (Exception e) {
            continue;
          }

          foreach (var type in types)
          {
            if (typeof (IBulkAction).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
              var text = "";
              try
              {
                text =
                  type.GetProperty("BulkText")
                    .GetValue(FormatterServices.GetUninitializedObject(type), null)
                    .ToString();
              }
              catch (Exception)
              {
                try
                {
                  text =
                    type.GetProperty("Text")
                      .GetValue(FormatterServices.GetUninitializedObject(type), null)
                      .ToString();
                }
                catch (Exception)
                {
                  // ignored
                }
              }
              var actionElement = new XElement("li", text + Environment.NewLine,
                new XComment(type.FullName),
                XmlHelpers.CreateInclude("Fix_in_Scope_Static_Chunks", type.FullName.NormalizeStringForAttribute()));
              var lang = GeneralHelpers.TryGetLang(type.FullName);

              if (typeof (IContextAction).IsAssignableFrom(type))
              {
                caListByLang.Add(new Tuple<string, XElement>(lang, actionElement));
                if (!caLists.ContainsKey(lang))
                  caLists.Add(lang, new XElement("list"));
              }

              if (typeof (IQuickFix).IsAssignableFrom(type))
              {
                qfListByLang.Add(new Tuple<string, XElement>(lang, actionElement));
                if (!qfLists.ContainsKey(lang))
                  qfLists.Add(lang, new XElement("list"));
              }
            }
          }
        }

        foreach (var list in qfLists)
        {
          foreach (var qfListItem in qfListByLang)
          {
            if (list.Key == qfListItem.Item1)
              list.Value.Add(qfListItem.Item2);
          }
          var langChapter = XmlHelpers.CreateChapter(list.Key);
          langChapter.Add(list.Value);
          qfChunk.Add(langChapter);
        }

        foreach (var list in caLists)
        {
          foreach (var qfListItem in caListByLang)
          {
            if (list.Key == qfListItem.Item1)
              list.Value.Add(qfListItem.Item2);
          }
          var langChapter = XmlHelpers.CreateChapter(list.Key);
          langChapter.Add(list.Value);
          caChunk.Add(langChapter);
        }

        inScopeLibrary.Root.Add(new XComment("Total quick-fix in scope: " + qfListByLang.Count()));
        inScopeLibrary.Root.Add(new XComment("Total context actions in scope: " + caListByLang.Count()));

        inScopeLibrary.Root.Add(qfChunk);

        inScopeLibrary.Root.Add(caChunk);

        inScopeLibrary.Save(Path.Combine(saveDirectoryPath, caTopicId + ".xml"));
        MessageBox.ShowInfo("Fix in scope actions imported successfully");
      }
    }
  }
}