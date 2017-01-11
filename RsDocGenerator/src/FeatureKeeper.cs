using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;

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
       //     AddExternalWikiLinks();
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

        public void AddFeatures(FeatureCatalog featureCatalog)
        {
            var totalFeatures = 0;
            var totalFeaturesInVersion = 0;

            foreach (var lang in featureCatalog.Languages)
            {
                var langPresentation = GeneralHelpers.GetPsiLanguagePresentation(lang);
                var langImplementations = featureCatalog.GetLangImplementations(lang);
                totalFeatures += langImplementations.Count;
                var featureRootNodeName = featureCatalog.FeatureKind + "Node";
                var totalLangFeaturesInVersion = 0;

                var existingLangFeatures = (from el in _catalogDocument.Root.Descendants("lang")
                        where (string) el.Attribute("name") == langPresentation
                        select el).Descendants(featureCatalog.FeatureKind.ToString())
                    .Select(e => e.Attribute("id").Value)
                    .ToList();

                var langElement = (from el in _currentVersionElement.Elements("lang")
                                      where (string) el.Attribute("name") == langPresentation
                                      select el).FirstOrDefault() ??
                                  new XElement("lang", new XAttribute("name", langPresentation));

                if (langElement.Element(featureRootNodeName) != null)
                    continue;
                var featuresRootElemnt = new XElement(featureRootNodeName);


                foreach (var feature in langImplementations)
                {
                    if (existingLangFeatures.Contains(feature.Id)) continue;

                    featuresRootElemnt.Add(new XElement(featureCatalog.FeatureKind.ToString(),
                        new XAttribute("id", feature.Id),
                        new XAttribute("text", feature.Text)));
                    totalLangFeaturesInVersion += 1;
                    totalFeaturesInVersion += 1;
                }

                if (featuresRootElemnt.HasElements)
                {
                    featuresRootElemnt.Add(new XAttribute("total",
                        existingLangFeatures.Count + totalLangFeaturesInVersion));
                    featuresRootElemnt.Add(new XAttribute("new", totalLangFeaturesInVersion));
                    langElement.Add(featuresRootElemnt);
                }

                if (!langElement.HasElements) continue;

                if ((from el in _currentVersionElement.Elements("lang")
                        where (string) el.Attribute("name") == langPresentation
                        select el).FirstOrDefault() == null)
                    _currentVersionElement.Add(langElement);
            }
            var statNode = _currentVersionElement.Element("statistics");
            if (statNode == null)
            {
                statNode = new XElement("statistics");
                _currentVersionElement.AddFirst(statNode);
            }
            if (statNode.Element(featureCatalog.FeatureKind.ToString()) != null) return;
            statNode.Add(new XElement(featureCatalog.FeatureKind.ToString(),
                new XAttribute("total", totalFeatures),
                new XAttribute("new", totalFeaturesInVersion)
            ));
        }
    }
}