using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Resources.Shell;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
    [Action("RsDocExportCodeInspections", "Export Code Inspections", Id = 7009)]
    internal class RsDocExportCodeInspections : IExecutableAction
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            using (var brwsr = new FolderBrowserDialog {Description = "Choose where to save  the output files."})
            {
                const string inspectionsFileName = "Code_Inspections";
                if (brwsr.ShowDialog() == DialogResult.Cancel) return;
                var saveDirectoryPath = brwsr.SelectedPath;
                var fileName = Path.Combine(saveDirectoryPath, inspectionsFileName + ".xml");

                var allIds = new List<string>();
                var aaallIds = new List<string>();
                var duplicateIds = new List<Tuple<string, XElement>>();

                var inspectionTopic = new XDocument();
                var configurations = Shell.Instance.GetComponent<HighlightingSettingsManager>().SeverityConfigurations;

                var inspectionRootElement = new XElement("Insections");
                var cppInspectionsHtml = new XElement("Html");
                foreach (var inspection in configurations)
                {
                    if (inspection.Internal || inspection.Id.Contains("CppClangTidy"))
                        continue;

                    var inspectionId = inspection.Id;
                    var inspectionElement = new XElement("Inspection", inspection.Description);

                    inspectionElement.Add(new XAttribute("id", inspectionId));
                    inspectionElement.Add(new XAttribute("lang", GetLangsForInspection(inspectionId)));
                    inspectionElement.Add(new XAttribute("FullTitle", inspection.FullTitle));
                    inspectionElement.Add(new XAttribute("Title", inspection.Title));
//          inspectionElement.Add(new XAttribute("InsType", inspection.GetType().ToString()));
                    inspectionElement.Add(new XAttribute("Severity", inspection.DefaultSeverity));
                    inspectionElement.Add(new XAttribute("Group", inspection.GroupId));
                    inspectionElement.Add(new XAttribute("AppearedInVersion", GeneralHelpers.GetCurrentVersion()));

                    if (!allIds.Contains(inspectionId))
                        allIds.Add(inspectionId);
                    else
                        duplicateIds.Add(new Tuple<string, XElement>(inspectionId, inspectionElement));
                    inspectionRootElement.Add(inspectionElement);

                    var cppInspectionHtml = new XElement("tr", XElement.Parse("<td class='_no-highlighted'></td>"),
                        XElement.Parse("<td class='_icon-cross'>No matching functionality</td>"));
                    if (GetLangsForInspection(inspectionId) == "CPP")
                    {
                        cppInspectionHtml.Add(new XElement("td", new XAttribute("class", "_icon-check"),
                            inspection.FullTitle));
                        cppInspectionsHtml.Add(cppInspectionHtml);
                    }
                }


//        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
//        {
//          Type[] types;
//          try
//          {
//            types = assembly.GetTypes();
//          }
//          catch (Exception e)
//          {
//            continue;
//          }
//
//          foreach (var type in types)
//          {
//            if (typeof(IQuickFix).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
//            {
//              var text = "";
//              try
//              {
//                text =
//                    type.GetProperty("Text")
//                        .GetValue(FormatterServices.GetUninitializedObject(type), null)
//                        .ToString();
//              }
//              catch (Exception)
//              {
//                // ignored
//              }
//              if (text.IsNullOrEmpty())
//                text = RsDocExportCodeInspections.SplitCamelCase(type.Name);
//
//              var lang = GeneralHelpers.TryGetLang(type.FullName);
//              
//              var inspectionElement = new XElement("QuickFix");
//
//              inspectionElement.Add(new XAttribute("id", type.Name));
//              inspectionElement.Add(new XAttribute("lang", lang));
//              inspectionElement.Add(new XAttribute("FullTitle", text));
//
//              inspectionRootElement.Add(inspectionElement);
//            }
//          }
//        }


                var duplicatedElement = new XElement("duplicatedIds");

                foreach (var duplicateId in duplicateIds) duplicatedElement.Add(duplicateId);

                inspectionRootElement.Add(duplicatedElement);
                inspectionTopic.Add(inspectionRootElement);
                inspectionRootElement.Add(cppInspectionsHtml);
                inspectionTopic.Save(fileName);
                MessageBox.ShowInfo("Code inspections saved successfully");
            }
        }

        private string GetLangsForInspection(string id)
        {
            var lang = string.Empty;
            //var langs = HighlightingSettingsManager.Instance.GetConfigurableSeverityImplementations(id);
            var langs = HighlightingSettingsManager.Instance.GetInspectionImplementations(id);
            foreach (var psiLanguageType in langs)
            {
//        string langName = NormalizeLanguage(psiLanguageType.Name);
                var langName = psiLanguageType.Name;
                if (!lang.Contains(langName))
                    lang += langName + ",";
            }

            lang = lang == string.Empty ? "all" : lang.TrimEnd(',');
            return lang;
        }

        private static string SplitCamelCase(string input)
        {
            //      if (input.Contains("QuickFix")) input = input.Replace("QuickFix", "");
            input = input.Replace("QuickFix", "").Replace("Fix", "").Replace("Cpp", "");
            var splitString = Regex.Replace(input, "([A-Z])", " $1",
                RegexOptions.Compiled).Trim();
            return splitString.Substring(0, 1) + splitString.Substring(1).ToLower();
        }
    }
}