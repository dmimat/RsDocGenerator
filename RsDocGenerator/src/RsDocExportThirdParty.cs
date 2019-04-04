using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Util.Extension;

namespace RsDocGenerator
{
    [Action("RsDocExportThirdParty", "Export Third-Party Libraries", Id = 1327)]
    internal class RsDocExportThirdParty : RsDocExportBase
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            var rootFolder = GeneralHelpers.GetDotnetDocsRootFolder(context);
            if (string.IsNullOrEmpty(rootFolder)) return "Nothing";
            var dinfo = new DirectoryInfo(rootFolder + "\\nonProject\\third-party");
            var files = dinfo.GetFiles("*.txt");

            var libraryList = new Dictionary<string, string>();

            foreach (var file in files)
            {
                if (file == null) continue;
                var productId = "";
                if (file.Name.Contains("dotCover.")) productId = "dcv";
                if (file.Name.Contains("dotMemory.")) productId = "dm";
                if (file.Name.Contains("dotMemoryUnit.")) productId = "dmu";
                if (file.Name.Contains("dotPeek.")) productId = "dpk";
                if (file.Name.Contains("dotTrace.")) productId = "dt";
                if (file.Name.Contains("ReSharper.")) productId = "rs";
                if (file.Name.Contains("ReSharperCpp.")) productId = "rcpp";
                if (file.Name.Contains("ReSharperCli.")) productId = "cli";
                if (file.Name.Contains("ReSharperHost.")) productId = "rshost";
                if (file.Name.Contains("teamCityAddin.")) productId = "tca";
//                if (file.Name.Contains("manual_additions")) productId = "man";
                if (file.Name.Contains("Rider")) productId = "rdr";

                var lines = File.ReadLines(file.FullName);
                foreach (var line in lines)
                {
                    if (line.Contains("Software || Version || License"))
                        continue;
                    if (libraryList.Keys.Contains(line))
                    {
                        libraryList[line] += "," + productId;
                        continue;
                    }

                    libraryList.Add(line, productId);
                }
            }

            const string thirdPartyTopicId = "Third_Party_Generated";
            var thirdPartyTopic = XmlHelpers.CreateHmTopic(thirdPartyTopicId, "Third-Party Libraries");

            var thirdPartyTable = new XElement("table");
            var headerRow = new XElement("tr");
            headerRow.Add(new XElement("td", "Software"));
            headerRow.Add(new XElement("td", "Version"));
            headerRow.Add(new XElement("td", "License"));
            headerRow.Add(new XElement("td", "Authors", new XAttribute("filter", "dotNet")));
            headerRow.Add(new XElement("td", "Copyright", new XAttribute("filter", "dotNet")));

            thirdPartyTable.Add(headerRow);

            var rows = new SortedList<string, XElement>();

            foreach (var lib in libraryList)
            {
                var libRow = new XElement("tr");
                libRow.Add(new XAttribute("filter", lib.Value));
                var libString = lib.Key;

                string[] removeStrings = {"JetBrains.", ".JetBrains", "JetBrains Platform ", "&nbsp;", "Â"};
                foreach (var str in removeStrings)
                {
                    var index = libString.IndexOf(str, StringComparison.Ordinal);
                    libString = index < 0 ? libString : libString.Remove(index, str.Length);
                }

                var splitterPos = new List<int>();
                var bracketCount = 0;
                for (var i = 0; i < libString.Length; i++)
                {
                    var ch = libString[i];
                    if (ch == '[')
                        bracketCount++;
                    if (ch == ']')
                        bracketCount--;
                    if (ch == '|' && bracketCount == 0)
                        splitterPos.Add(i);
                }

                for (var i = 0; i < splitterPos.Count - 1; i++)
                {
                    var cellContent = libString.Substring(splitterPos[i] + 1,
                        splitterPos[i + 1] - splitterPos[i] - 1);
                    var td = new XElement("td", TryGetHyperLink(cellContent));
                    if (i > 2)
                        td.Add(new XAttribute(new XAttribute("filter", "dotNet")));
                    libRow.Add(td);
                }

                rows.Add(libString.TrimStart('|').TrimStart('['), libRow);
            }

            foreach (var libRow in rows.Values)
                thirdPartyTable.Add(libRow);

            var thirdPartyChunk = XmlHelpers.CreateChunk("table");
            thirdPartyChunk.Add(thirdPartyTable);
            thirdPartyTopic.Root.Add(thirdPartyChunk);

            thirdPartyTopic.Save(Path.Combine(outputFolder.AddGeneratedPath(), thirdPartyTopicId + ".xml"));
            return "Third-Party Libraries";
        }

        private static object TryGetHyperLink(string text)
        {
            var linkText = "License";
            if (text.IsNullOrEmpty() || !text.StartsWith("["))
                return text;

            var parts = text.Trim('[', ']').Split('|');
            string linkUrl;

            if (parts.Length == 1)
            {
                linkUrl = parts[0];
                if (!linkUrl.Contains("http"))
                    return linkUrl;
            }
            else
            {
                linkText = parts[0];
                linkUrl = parts[1];
            }

            if (linkUrl == "https://www.devexpress.com/Products/NET/Controls/WinForms/")
                linkText = "DevExpress WinForms Controls and Libraries";

            return XmlHelpers.CreateHyperlink(linkText, linkUrl, null, false);
        }
    }
}