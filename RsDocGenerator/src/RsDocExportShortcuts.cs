using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Util;
using JetBrains.UI.ActionsRevised;
using JetBrains.UI.ActionsRevised.Loader;
using JetBrains.Util;
using JetBrains.Util.dataStructures.Sources;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
  [ShellComponent]
  [Action("RsDocExportShortcuts", "Export Actions and Shortcuts", Id = 8373)]
  public class RsDocExportShortcuts : IExecutableAction
  {
    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return true;
    }

    public void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      using (var brwsr = new FolderBrowserDialog {Description = "Choose where to save XML topics."})
      {
        if (brwsr.ShowDialog() == DialogResult.Cancel) return;
        string saveDirectoryPath = brwsr.SelectedPath;
        var shortcutsXmlDoc = new XDocument();
        var actionMapElement = new XElement("Keymap");
        shortcutsXmlDoc.Add(actionMapElement);
        XmlHelpers.AddAutoGenComment(shortcutsXmlDoc.Root);

        const string menuPathLibId = "Menupath_by_ID";
        string menupathLibfileName = Path.Combine(saveDirectoryPath, menuPathLibId + ".xml");
        var menuPathLibrary = XmlHelpers.CreateHmTopic(menuPathLibId);
        const string accessIntroLibId = "AccessIntro_by_ID";
        string accessIntroLibfileName = Path.Combine(saveDirectoryPath, accessIntroLibId + ".xml");
        var accessIntroLibrary = XmlHelpers.CreateHmTopic(accessIntroLibId);

        var actionManager = context.GetComponent<IActionManager>();

        var productcatalogs = context.GetComponent<IPartCatalogSet>();
        var actionParts = PartSelector.LeafsAndHides.SelectParts(
          productcatalogs.Catalogs.SelectMany(catalog => catalog.GetPartsWithAttribute<ActionAttribute>().ToEnumerable()))
          .ToList();

        foreach (PartCatalogType actionPart in actionParts)
        {
          var attributes = actionPart.GetPartAttributes<ActionAttribute>();

          if (attributes.Count == 0)
            Assertion.Fail("{0} has no ActionAttribute", actionPart);

          if (attributes.Count > 1)
            Assertion.Fail("{0} has {1} ActionAttribute annotations. Only one annotation is supported.",
              actionPart, attributes.Count);

          PartCatalogAttribute attribute = attributes.GetItemAt(0);
          var actionId =
            (attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_ActionId].GetStringValueIfDefined() ??
             StringSource.Empty).ToRuntimeString();
          if (actionId.IsEmpty())
            actionId = ActionDefines.GetIdFromName(actionPart.LocalName);
          var actionText =
            (attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_Text].GetStringValueIfDefined() ??
             StringSource.Empty).ToRuntimeString();
          if (actionText.IsEmpty())
            actionText = actionId;
          actionText = actionText.Replace("&", String.Empty);
          var vsShortcuts =
            attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_VsShortcuts].GetArrayValueIfDefined();
          var ideaShortcuts =
            attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_IdeaShortcuts].GetArrayValueIfDefined();
          if (actionId.Equals("CompleteCodeBasic")) vsShortcuts = new object[] {"Control+Space"};
          if (actionId.Equals("CompleteStatement")) vsShortcuts = new object[] {"Control+Shift+Enter"};
          if (actionId.Equals("CompleteCodeBasic")) ideaShortcuts = new object[] {"Control+Space"};
          if (actionId.Equals("CompleteStatement")) ideaShortcuts = new object[] {"Control+Shift+Enter"};
          string pathToTheRoot;
          try
          {
            pathToTheRoot =
              (context.GetComponent<ActionPresentationHelper>()
                .GetPathPresentationToRoot(actionManager.Defs.GetActionDefById(actionId)));
          }
          catch (Exception e) {
            pathToTheRoot = "";
          }
          if (!pathToTheRoot.IsEmpty() && !actionText.IsEmpty())
            pathToTheRoot = pathToTheRoot.Replace('→', '|') + " | " + actionText;
          var actionElement = new XElement("Action");
          var pattern = new Regex("[_.]");

          actionId = pattern.Replace(actionId, String.Empty);
          
          actionElement.Add(
            new XAttribute("id", actionId),
            new XAttribute("title", actionText),
            new XAttribute("menupath", pathToTheRoot));
          var accessIntroChunk = XmlHelpers.CreateChunk(actionId);
          var accessIntroWrapper = new XElement("p");
          if (!pathToTheRoot.IsNullOrEmpty())
          {
            var menupPathChunk = XmlHelpers.CreateChunk(actionId);
            pathToTheRoot =
              pathToTheRoot.Replace("ReSharper | Navigate ", "%navigateMenu%")
                .Replace("ReSharper | Windows ", "%windowsMenu%");
            menupPathChunk.Add(new XElement("menupath", pathToTheRoot));
            accessIntroWrapper.Add(new XElement("menupath", pathToTheRoot));
            accessIntroWrapper.Add(new XElement("br"));
            menuPathLibrary.Root.Add(menupPathChunk);
          }
          if (ideaShortcuts != null || vsShortcuts != null)
          {
            accessIntroWrapper.Add(new XElement("shortcut", new XAttribute("key", actionId)));
            accessIntroWrapper.Add(new XElement("br"));
          }
          accessIntroWrapper.Add(new XElement("code", String.Format("ReSharper_{0}", actionId),
            new XAttribute("product", "rs")));
          accessIntroChunk.Add(accessIntroWrapper);
          accessIntroLibrary.Root.Add(accessIntroChunk);
          
          AddShortcuts(ideaShortcuts, actionElement, "rs");
          AddShortcuts(vsShortcuts, actionElement, "vs");

          if (actionId == "ParameterInfoShow")
            actionElement.Add(new XElement("Shortcut", "Control+Shift+Space", new XAttribute("layout", "vs")));
          if (actionId == "GotoDeclaration")
            actionElement.Add(new XElement("Shortcut", "F12", new XAttribute("layout", "vs")));
          if (actionId == "GotoImplementation")
            actionElement.Add(new XElement("Shortcut", "Ctrl+F12", new XAttribute("layout", "vs")));
          // TODO: check if this hack works 

          actionMapElement.Add(actionElement);
        }

        shortcutsXmlDoc.Save(Path.Combine(saveDirectoryPath, "keymap.xml"));
        menuPathLibrary.Save(menupathLibfileName);
        accessIntroLibrary.Save(accessIntroLibfileName);

        MessageBox.ShowInfo("Shortcuts and Actions exported successfully");
      }
    }

    private static void AddShortcuts(object[] currentShortcutsSet, XElement actionElement, string keymapName)
    {
      if (currentShortcutsSet != null)
      {
        string previousKeystroke = "UNDEFINED";
        foreach (var keystroke in currentShortcutsSet)
        {
          string curretnKeystroke = keystroke.ToString();
          // replace shortcuts like Control+K, Control+Z with Control+K Z
          const string control = "Control+";
          if (curretnKeystroke.Split('+').Count(x => x.Contains("Control")) == 2)
          {
            int place = curretnKeystroke.LastIndexOf(control);
            curretnKeystroke = curretnKeystroke.Remove(place, control.Length).Insert(place, "");
          }
          const string alt = "Alt+";
          if (curretnKeystroke.Split('+').Count(x => x.Contains("Alt")) == 2)
          {
            int place = curretnKeystroke.LastIndexOf(alt);
            curretnKeystroke = curretnKeystroke.Remove(place, alt.Length).Insert(place, "");
          }
          curretnKeystroke = curretnKeystroke.Replace("D1", "1");
          curretnKeystroke = curretnKeystroke.Replace("D2", "2");
          curretnKeystroke = curretnKeystroke.Replace("D3", "3");
          curretnKeystroke = curretnKeystroke.Replace("D4", "4");
          curretnKeystroke = curretnKeystroke.Replace("D5", "5");
          // drop same shortcuts
          //                    var exists = (from nodes in actionElement.Elements()
          //                        where nodes.Value == curretnKeystroke
          //                        select nodes).FirstOrDefault();
          if (!previousKeystroke.Equals(curretnKeystroke)) {
            actionElement.Add(new XElement("Shortcut", curretnKeystroke, new XAttribute("layout", keymapName)));
          }
          previousKeystroke = curretnKeystroke;
        }
      }
    }


    //        private void AddKeymap(string keymapName, string keymapAttrName, XElement keymapsElement)
    //        {
    //            var keymapElement = new XElement("Keymap", new XAttribute("name", keymapName));
    //            var actionManager = Shell.Instance.GetComponent<IActionManager>();
    //
    //            foreach (var actionPart in _actionParts)
    //            {
    //                var attributes = actionPart.GetAttributes<ActionAttribute>().ToList();
    //
    //                if (attributes.Count == 0)
    //                    Assertion.Fail("{0} has no ActionAttribute", actionPart);
    //
    //                if (attributes.Count > 1)
    //                    Assertion.Fail("{0} has {1} ActionAttribute annotations. Only one annotation is supported.",
    //                        actionPart, attributes.Count);
    //
    //                var attribute = attributes[0];
    //                var actionId = attribute.TryGetProperty<string>(ActionAttribute.ActionAttribute_ActionId);
    //                if (actionId.IsEmpty())
    //                    actionId = ActionDefines.GetIdFromName(actionPart.LocalName);
    //                var actionText = (attribute.TryGetProperty<string>(ActionAttribute.ActionAttribute_Text));
    //                if (!actionText.IsNullOrEmpty())
    //                    actionText = actionText.Replace("&", String.Empty);
    //                else
    //                    actionText = "";
    //                var vsShortcuts = attribute.TryGetProperty<object[]>(keymapAttrName);
    //                if (actionId.Equals("CompleteCodeBasic")) vsShortcuts = new object[] { "Control+Space" };
    //                if (actionId.Equals("CompleteStatement")) vsShortcuts = new object[] { "Control+Shift+Enter" };
    //                string pathToTheRoot = "";
    //                try
    //                {
    //                    pathToTheRoot =
    //                        (Shell.Instance.GetComponent<ActionPresentationHelper>()
    //                            .GetPathPresentationToRoot(actionManager.Defs.GetActionDefById(actionId)));
    //                }
    //                catch (Exception e)
    //                {
    //                    pathToTheRoot = String.Empty;
    //                }
    //                if(!pathToTheRoot.IsEmpty() && !actionText.IsEmpty())
    //                    pathToTheRoot = pathToTheRoot.Replace('→', '|') + " | " + actionText;
    //                var actionElement = new XElement("Action");
    //                Regex pattern = new Regex("[_.]");
    //                actionElement.Add(
    //                    new XAttribute("id", pattern.Replace(actionId, "")),
    //                    new XAttribute("title", actionText),
    //                    new XAttribute("menupath", pathToTheRoot));
    //                if (vsShortcuts != null)
    //                    foreach (var keystroke in vsShortcuts)
    //                {
    //                    string curretnKeystroke = keystroke.ToString();
    //                    // replace shortcuts like Control+K, Control+Z with Control+K Z
    //                    const string control = "Control+";
    //                    if (curretnKeystroke.Split('+').Count(x => x.Contains("Control")) == 2)
    //                    {
    //                        int place = curretnKeystroke.LastIndexOf(control);
    //                        curretnKeystroke = curretnKeystroke.Remove(place, control.Length).Insert(place, "");
    //                    }
    //                    const string alt = "Alt+";
    //                    if (curretnKeystroke.Split('+').Count(x => x.Contains("Alt")) == 2)
    //                    {
    //                        int place = curretnKeystroke.LastIndexOf(alt);
    //                        curretnKeystroke = curretnKeystroke.Remove(place, alt.Length).Insert(place, "");
    //                    }
    //                    // drop same shortcuts
    //                    var exists = (from nodes in actionElement.Elements()
    //                        where nodes.Value == curretnKeystroke
    //                        select nodes).FirstOrDefault();
    //                    if (exists == null)
    //                    {
    //                        curretnKeystroke = curretnKeystroke.Replace("D1", "1");
    //                        curretnKeystroke = curretnKeystroke.Replace("D2", "2");
    //                        curretnKeystroke = curretnKeystroke.Replace("D3", "3");
    //                        curretnKeystroke = curretnKeystroke.Replace("D4", "4");
    //                        curretnKeystroke = curretnKeystroke.Replace("D5", "5");
    //                        actionElement.Add(new XElement("Shortcut", curretnKeystroke));
    //                    }
    //                }
    //
    //
    //                keymapElement.Add(actionElement);               
    //            }
    //            keymapsElement.Add(keymapElement);
    //        }
  }
}