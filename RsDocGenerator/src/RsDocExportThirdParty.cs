using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.UI.ActionsRevised;
using JetBrains.Util.Extension;

namespace RsDocGenerator
{
    [Action("RsDocExportThirdParty", "Export Third-Party Libraries", Id = 1327)]
    internal class RsDocExportThirdParty : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
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
                if(file == null) continue;
                var productId = "";
                if (file.Name.Contains("dotCover.")) productId = "dcv";
                if (file.Name.Contains("dotMemory.")) productId = "dm";
                if (file.Name.Contains("dotPeek.")) productId = "dpk";
                if (file.Name.Contains("dotTrace.")) productId = "dt";
                if (file.Name.Contains("ReSharper.")) productId = "rs";
                if (file.Name.Contains("ReSharperCpp.")) productId = "rcpp";
                if (file.Name.Contains("ReSharperCli.")) productId = "cli";
                if (file.Name.Contains("ReSharperHost.")) productId = "rshost";
                if (file.Name.Contains("teamCityAddin.")) productId = "tca";
                if (file.Name.Contains("manual_additions")) productId = "man";
                if (file.Name.Contains("Rider")) productId = "rdr";
                
                var lines = File.ReadLines(file.FullName);
                foreach (var line in lines)
                {
                    if (line.Contains("Software || Version || License"))
                        continue;
                    if (libraryList.Keys.Contains(line))
                    {
                        libraryList[line] += ("," + productId);
                        continue;
                    }
                    libraryList.Add(line, productId);
                }
            }
            
            const string thirdPartyTopicId = "Third_Party_Generated";
            var thirdPartyTopic = XmlHelpers.CreateHmTopic(thirdPartyTopicId, "Third-Party Libraries");
            
            var thirdPartyTable = XmlHelpers.CreateTable(new[] {"Software", "Version", "License"}, null);

            foreach (var lib in libraryList)
            {
                var libRow = new XElement("tr");
                libRow.Add(new XAttribute("filter", lib.Value));
                var libString = lib.Key;
                
                string [] removeStrings = {"JetBrains.", ".JetBrains", "&nbsp;"};
                foreach (var str in removeStrings)
                {
                    int index = libString.IndexOf(str, StringComparison.Ordinal);
                    libString = (index < 0) ? libString : libString.Remove(index, str.Length);
                }
                
                var splitterPos = new List<int>();
                int bracketCount = 0;
                for (var i = 0; i < libString.Length; i++)
                {
                    char ch = libString[i];
                    if (ch == '[')
                        bracketCount++;
                    if (ch == ']')
                        bracketCount--;
                    if (ch == '|' && bracketCount == 0)
                        splitterPos.Add(i);
                }
                string sw = libString.Substring(splitterPos[0] + 1, splitterPos[1] - splitterPos[0] - 1);
                string ver = libString.Substring(splitterPos[1] + 1, splitterPos[2] - splitterPos[1] - 1);
                string lis = libString.Substring(splitterPos[2] + 1, splitterPos[3] - splitterPos[2] -1);

                libRow.Add(new XElement("td", TryGetHyperLink(sw, "Software")));
                libRow.Add(new XElement("td", TryGetHyperLink(ver, "Version")));
                libRow.Add(new XElement("td", TryGetHyperLink(lis, "License")));
                thirdPartyTable.Add(libRow);
            }
            var thirdPartyChunk = XmlHelpers.CreateChunk("table");
            thirdPartyChunk.Add(thirdPartyTable);
            thirdPartyTopic.Root.Add(thirdPartyChunk);

            thirdPartyTopic.Save(Path.Combine(outputFolder, thirdPartyTopicId + ".xml"));
            return "Third-Party Libraries";
        }

        private static object TryGetHyperLink(string text, string linkText)
        {
            if (text.IsNullOrEmpty() || !text.StartsWith("["))
                return text;

            var parts = text.Trim('[', ']').Split('|');
            string linkUrl;

            if (parts.Length == 1)
            {
                linkUrl = parts[0];
            }
            else
            {
                linkText = parts[0];
                linkUrl = parts[1];
            }

            return XmlHelpers.CreateHyperlink(linkText, linkUrl, null, false);
        }
    }
}