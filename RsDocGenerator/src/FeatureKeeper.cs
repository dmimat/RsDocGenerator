using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Build.Serialization;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace RsDocGenerator
{
    public sealed class FeatureKeeper
    {
        private readonly string _catalogFile;
        private readonly XDocument _catalogDocument;
        private const string FileName = "RsFeatureCatalog.xml";
        private const string RootNodeName = "RsFeatureCatalog";
        private const string VersionElementName = "version";
        private const string Externalwikilinks = "ExternalWikiLinks";
        private readonly XElement _currentVersionElement;


        public FeatureKeeper(IDataContext context)
        {
            _catalogFile = Path.Combine(GeneralHelpers.GetFeatureCatalogFolder(context) ?? Path.GetTempPath(),
                FileName);
            if (File.Exists(_catalogFile))
            {
                try
                {
                    _catalogDocument = XDocument.Load(_catalogFile);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else
            {
                _catalogDocument = new XDocument();
                _catalogDocument.Add(new XElement(RootNodeName));
            }
            var currentVersionString = GeneralHelpers.GetCurrentVersion();
            _currentVersionElement = (from el in _catalogDocument.Root.Elements(VersionElementName)
                where (string) el.Attribute("v") == currentVersionString
                select el).FirstOrDefault();
            if (_currentVersionElement == null)
            {
                _currentVersionElement = new XElement(VersionElementName, new XAttribute("v", currentVersionString));
                _catalogDocument.Root.AddFirst(_currentVersionElement);
            }
        }

        public void CloseSession()
        {
            AddExternalWikiLinks();
            _catalogDocument.Save(_catalogFile);
        }

        private void AddExternalWikiLinks()
        {
            var wiki = _catalogDocument.Root.Element(Externalwikilinks);
            if (wiki != null) wiki.Remove();

            wiki = new XElement(Externalwikilinks);
            foreach (var item in CodeInspectionHelpers.ExternalInspectionLinks)
            {
                var link = item.Value;
                if (!link.Contains("http"))
                    link = "http://www.jetbrains.com/resharperplatform/help?Keyword=" + link;
                wiki.Add(new XElement("Item",
                    new XAttribute("Id", item.Key),
                    new XAttribute("Url", link)));
            }
            _catalogDocument.Root.Add(wiki);
        }

        public void AddFeatures(Dictionary<PsiLanguageType, FeaturesByLanguageGroup> groupsByLanguage)
        {
            var totalFeatures = 0;
            var totalFeaturesInVersion = 0;
            var featureKind = groupsByLanguage.First().Value.FeatureKind;

            foreach (var langGroup in groupsByLanguage.Values)
            {
                totalFeatures += langGroup.TotalFeatures();
                var featureRootNodeName = featureKind + "Node";
                var totalLangFeaturesInVersion = 0;

                var existingLangFeatures = (from el in _catalogDocument.Root.Descendants("lang")
                    where (string) el.Attribute("name") == langGroup.Name
                    select el).Descendants(featureKind.ToString())
                    .Select(e => e.Attribute("id").Value)
                    .ToList();

                var langElement = (from el in _currentVersionElement.Elements("lang")
                                      where (string) el.Attribute("name") == langGroup.Name
                                      select el).FirstOrDefault() ??
                                  new XElement("lang", new XAttribute("name", langGroup.Name));

                if (langElement.Element(featureRootNodeName) != null)
                    continue;
                var featuresRootElemnt = new XElement(featureRootNodeName);

                foreach (var category in langGroup.Categories)
                {
                    foreach (var feature in category.Value.Inspections)
                    {
                        if (existingLangFeatures.Contains(feature.Id)) continue;

                        featuresRootElemnt.Add(new XElement(featureKind.ToString(),
                            new XAttribute("id", feature.Id),
                            new XAttribute("name", feature.Name)));
                        totalLangFeaturesInVersion += 1;
                        totalFeaturesInVersion += 1;
                    }
                }
                if (featuresRootElemnt.HasElements)
                {
                    featuresRootElemnt.Add(new XAttribute("total", existingLangFeatures.Count + totalLangFeaturesInVersion));
                    featuresRootElemnt.Add(new XAttribute("new", totalLangFeaturesInVersion));
                    langElement.Add(featuresRootElemnt);
                }

                if (!langElement.HasElements) continue;

                if ((from el in _currentVersionElement.Elements("lang")
                        where (string) el.Attribute("name") == langGroup.Name
                        select el).FirstOrDefault() == null)
                    _currentVersionElement.Add(langElement);
            }
            var statNode = _currentVersionElement.Element("statistics");
            if (statNode == null)
            {
                statNode = new XElement("statistics");
                _currentVersionElement.AddFirst(statNode);
            }
            if(statNode.Element(featureKind.ToString()) != null) return;
            statNode.Add(new XElement(featureKind.ToString(),
                new XAttribute("total", totalFeatures),
                new XAttribute("new", totalFeaturesInVersion)
            ));
        }
    }
}