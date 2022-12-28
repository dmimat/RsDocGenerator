using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions.ActionManager;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Loader;
using JetBrains.Application.UI.ActionSystem.UserPresentation;
using JetBrains.Diagnostics;
using JetBrains.Util;
using JetBrains.Util.dataStructures.Sources;

namespace RsDocGenerator
{
    [ShellComponent]
    [Action("RsDocExportShortcuts", "Export Actions and Shortcuts", Id = 8373)]
    public class RsDocExportShortcuts : RsDocExportBase
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            var shortcutsXmlDoc = new XDocument();
            var actionMapElement = new XElement("Keymap");
            shortcutsXmlDoc.Add(actionMapElement);
            XmlHelpers.AddAutoGenComment(shortcutsXmlDoc.Root);

            const string menuPathLibId = "Menupath_by_ID";
            var menuPathLibrary = XmlHelpers.CreateHmTopic(menuPathLibId, "Menupath_by_ID Chunks");
            const string accessIntroLibId = "AccessIntro_by_ID";
            var accessIntroLibrary = XmlHelpers.CreateHmTopic(accessIntroLibId, "AccessIntro_by_ID Chunks");

            var actionManager = context.GetComponent<IActionManager>();

            var productcatalogs = context.GetComponent<IPartCatalogSet>();
            var actionParts = PartSelector.LeafsAndHides.SelectParts(
                    productcatalogs.Catalog.GetPartsWithAttribute<ActionAttribute>().ToEnumerable().OrderBy(x => x.LocalName))
                .ToList();

            foreach (var actionPart in actionParts)
            {
                var attributes = actionPart.GetPartAttributes<ActionAttribute>();

                if (attributes.Count == 0)
                    Assertion.Fail("{0} has no ActionAttribute", actionPart);

                if (attributes.Count > 1)
                    Assertion.Fail("{0} has {1} ActionAttribute annotations. Only one annotation is supported.",
                        actionPart, attributes.Count);

                var attribute = attributes.GetItemAt(0);
                var actionId =
                    (attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_ActionId].GetStringValueIfDefined() ??
                     StringSource.Empty).ToRuntimeString();
                if (actionId.IsEmpty())
                    actionId = ActionDefines.GetIdFromName(actionPart.LocalName);
                var actionText = string.Empty;
                try
                {
                    actionText = actionManager.Defs.GetActionDefById(actionId).Text.Replace("&", string.Empty);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                    /*(attribute.ArgumentsOptional[ActionAttribute.ActionAttribute_Text].GetStringValueIfDefined() ??
                     StringSource.Empty).ToRuntimeString();*/
                if (actionText.IsEmpty())
                    actionText = actionId;
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
                        context.GetComponent<IActionPresentableTexts>().
                            GetPathToMenuRoot(actionManager.Defs.GetActionDefById(actionId));
                }
                catch (Exception e)
                {
                    pathToTheRoot = "";
                }

                if (!pathToTheRoot.IsEmpty() && !actionText.IsEmpty())
                    pathToTheRoot = pathToTheRoot.Replace('→', '|') + " | " + actionText;
                var actionElement = new XElement("Action");
                var pattern = new Regex("[_.]");

                actionId = pattern.Replace(actionId, string.Empty);

                actionElement.Add(
                    new XAttribute("id", actionId),
                    new XAttribute("title", actionText),
                    new XAttribute("menupath", pathToTheRoot));
                var accessIntroChunk = XmlHelpers.CreateChunk(actionId);
                var accessIntroWrapper = new XElement("microformat");
                if (!pathToTheRoot.IsNullOrEmpty())
                {
                    var menuPathChunk = XmlHelpers.CreateChunk(actionId);
                    pathToTheRoot = pathToTheRoot
                            .Replace("ReSharper | Navigate ", "%navigateMenu%")
                            .Replace("ReSharper | Windows ", "%windowsMenu%")
                            .Replace("ReSharper | Edit ", "%editMenu%");
                    menuPathChunk.Add(new XElement("menupath", pathToTheRoot));
                    accessIntroWrapper.Add(new XElement("p", new XElement("menupath", pathToTheRoot)));
                    menuPathLibrary.Root.Add(menuPathChunk);
                }
                
                var shortcutWrapper = new XElement("p");
                
                if (ideaShortcuts != null || vsShortcuts != null)
                {
                    shortcutWrapper.Add(new XElement("shortcut", new XAttribute("key", actionId)));
                    shortcutWrapper.Add(XElement.Parse($"<for product=\"rs,dcv\">(<code>ReSharper_{actionId}</code>)</for>"));
                }
                else
                {
                    var assignTip = XmlHelpers.CreateInclude("Tips", "assign_shortcut_raw");
                    assignTip.Add(XmlHelpers.CreateVariable("actionId",actionId));
                    shortcutWrapper.Add(
                        assignTip,
                        new XAttribute("product", "rs,dcv"));
                }

                accessIntroWrapper.Add(shortcutWrapper);
                accessIntroChunk.Add(accessIntroWrapper);
                accessIntroLibrary.Root.Add(accessIntroChunk);

                AddShortcuts(ideaShortcuts, actionElement, "rs");
                AddShortcuts(vsShortcuts, actionElement, "vs");
                if (actionId == "GotoImplementations")
                    AddShortcuts(new object[] {"Ctrl+F12"}, actionElement, "vs");

                if (actionId == "ParameterInfoShow")
                    actionElement.Add(new XElement("Shortcut", "Control+Shift+Space", new XAttribute("layout", "vs")));
                if (actionId == "GotoDeclaration")
                    actionElement.Add(new XElement("Shortcut", "F12", new XAttribute("layout", "vs")));
                if (actionId == "GotoImplementation")
                    actionElement.Add(new XElement("Shortcut", "Ctrl+F12", new XAttribute("layout", "vs")));
                // TODO: check if this hack works 

                actionMapElement.Add(actionElement);
            }

            var generatedFolder = outputFolder.AddGeneratedPath();
            shortcutsXmlDoc.Save(Path.Combine(outputFolder, "keymap.xml"));
            menuPathLibrary.Save(Path.Combine(generatedFolder, menuPathLibId + ".xml"));
            accessIntroLibrary.Save(Path.Combine(generatedFolder, accessIntroLibId + ".xml"));
            return "Shortcuts and actions";
        }

        private static void AddShortcuts(object[] currentShortcutsSet, XElement actionElement, string keymapName)
        {
            if (currentShortcutsSet != null)
            {
                var previousKeystroke = "UNDEFINED";
                foreach (var keystroke in currentShortcutsSet)
                {
                    var currentKeystroke = keystroke.ToString();
                    // replace shortcuts like Control+K, Control+Z with Control+K Z
                    const string control = "Control+";
                    if (currentKeystroke.Split('+').Count(x => x.Contains("Control")) == 2)
                    {
                        var place = currentKeystroke.LastIndexOf(control);
                        currentKeystroke = currentKeystroke.Remove(place, control.Length).Insert(place, "");
                    }

                    const string alt = "Alt+";
                    if (currentKeystroke.Split('+').Count(x => x.Contains("Alt")) == 2)
                    {
                        var place = currentKeystroke.LastIndexOf(alt);
                        currentKeystroke = currentKeystroke.Remove(place, alt.Length).Insert(place, "");
                    }

                    currentKeystroke = currentKeystroke.Replace("D1", "1");
                    currentKeystroke = currentKeystroke.Replace("D2", "2");
                    currentKeystroke = currentKeystroke.Replace("D3", "3");
                    currentKeystroke = currentKeystroke.Replace("D4", "4");
                    currentKeystroke = currentKeystroke.Replace("D5", "5");
                    // drop same shortcuts
                    //                    var exists = (from nodes in actionElement.Elements()
                    //                        where nodes.Value == curretnKeystroke
                    //                        select nodes).FirstOrDefault();
                    if (!previousKeystroke.Equals(currentKeystroke))
                        actionElement.Add(new XElement("Shortcut", currentKeystroke,
                            new XAttribute("layout", keymapName)));
                    previousKeystroke = currentKeystroke;
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