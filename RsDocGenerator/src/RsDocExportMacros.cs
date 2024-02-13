using System.Text.RegularExpressions;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.I18n;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Macros;
using JetBrains.ReSharper.LiveTemplates;
using JetBrains.ReSharper.Resources.Shell;

namespace RsDocGenerator
{
    [Action("RsDocExportMacros", "Export Template Macros", Id = 7969)]
    internal class RsDocExportMacros : RsDocExportBase, IAction
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            var macroLibrary = new HelpTopic("Template_Macros", "List of template macros", outputFolder.AddGeneratedPath() + "\\CodeTemplates");
            var macroChunk = XmlHelpers.CreateChunk("macro_table");
            var macroTable = XmlHelpers.CreateTable(new[] {"Expression", "Description", "Details"}, null);
            var macros = Shell.Instance.GetComponent<MacroManager>().Definitions;
            foreach (var macroDefinition in macros)
            {
                var macroRow = new XElement("tr");
                var customAttribute = MacroDescriptionFormatter.GetMacroAttribute(macroDefinition);
                var longDescription = JetResourceManager.GetString(customAttribute.ResourceType, customAttribute.LongDescriptionResourceName);
                var shortDescription = JetResourceManager.GetString(customAttribute.ResourceType, customAttribute.DescriptionResourceName);
                var macroId = MacroDescriptionFormatter.GetMacroAttribute(macroDefinition).Name;

                const string paramMatch = @"{#0:(.*)}";
                var parameters = macroDefinition.Parameters;

                MatchCollection paramNames = null;

                if (shortDescription != null)
                {
                    paramNames = Regex.Matches(shortDescription, paramMatch);
                    shortDescription = Regex.Replace(shortDescription, paramMatch, @"<b>$+</b>");
                }

                var expressionCellRaw = "<td><code>" + macroId + "()" + "</code></td>";
                var expressionCell = XElement.Parse(expressionCellRaw);
                expressionCell.Add(new XAttribute("id", macroId));

                var shortDescriptionCellRaw = "<td>" + shortDescription + "</td>";
                var shortDescriptionCell = XElement.Parse(shortDescriptionCellRaw);

                var paramNode = new XElement("list");

                if (paramNames != null)
                    for (var idx = 0; idx < paramNames.Count; idx++)
                    {
                        var parameterInfo = parameters[idx];
                        paramNode.Add(new XElement("li",
                            new XElement("b",
                                paramNames[idx].Groups[1].Value),
                            GetParameterAsString(parameterInfo.ParameterType)));
                    }

                var paramHeader = paramNode.HasElements ? new XElement("p", "Macro parameters:") : null;
                macroRow.Add(expressionCell,
                    shortDescriptionCell,
                    new XElement("td",
                        longDescription,
                        paramHeader,
                        paramNode,
                        XmlHelpers.CreateInclude("TR", "macros_" + macroId, true)));
                macroTable.Add(macroRow);
            }

            macroChunk.Add(macroTable);
            macroLibrary.Add(XmlHelpers.CreateInclude("Templates__Template_Basics__Template_Macros", "intro",
                false));
            macroLibrary.Add(new XElement("p", "Here is the full list of template macros provided by %product%:"));
            macroLibrary.Add(macroChunk);
            macroLibrary.Save();
            return "Template macros";
        }

        private static string GetParameterAsString(ParameterType param)
        {
            switch (param)
            {
                case ParameterType.String:
                    return " - text string";
                case ParameterType.Type:
                    return " - type";
                case ParameterType.VariableReference:
                    return " - reference to another parameter in the template";
            }

            return param.ToString();
        }
    }
}