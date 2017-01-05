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
        private const string ConfigurableInspectionsRootNodeName = "ConfigurableInspections";
        private const string StaticInspectionsRootNodeName = "StaticInspections";
        private const string ConfigurableInspectionNodeName = "CI";
        private const string StaticInspectionNodeName = "SI";
        private const string RootNodeName = "RsFeatureCatalog";
        private const string VersionElementName = "version";
        private const string Externalwikilinks = "ExternalWikiLinks";
        private readonly XElement _currentVersionElement;

        private Dictionary<RsSmallFeatureKind, List<string>> _existingFeatures =
            new Dictionary<RsSmallFeatureKind, List<string>>();

        private Dictionary<RsSmallFeatureKind, int> _totalFeatures = new Dictionary<RsSmallFeatureKind, int>();
        private Dictionary<RsSmallFeatureKind, int> _totalNewFeatures = new Dictionary<RsSmallFeatureKind, int>();

        public FeatureKeeper(IDataContext context)
        {
            _catalogFile = Path.Combine(GeneralHelpers.GetFeatureCatalogFolder(context) ?? Path.GetTempPath(),
                FileName);
            if (File.Exists(_catalogFile))
                _catalogDocument = XDocument.Load(_catalogFile);
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

            _existingFeatures.Add(RsSmallFeatureKind.ConfigInspection,
                _catalogDocument.Root.Descendants(ConfigurableInspectionNodeName)
                    .Select(e => e.Attribute("id").Value)
                    .ToList());
            _existingFeatures.Add(RsSmallFeatureKind.StaticInspection,
                _catalogDocument.Root.Descendants(StaticInspectionNodeName)
                    .Select(e => e.Attribute("id").Value)
                    .ToList());
        }

        public void CloseSession()
        {
          foreach (var newFeatures in _totalNewFeatures)
          {
            if (newFeatures.Value > 0)
            {
              var statNode = _currentVersionElement.Element("statistics");
              if (statNode == null)
              {
                statNode = new XElement("statistics");
                _currentVersionElement.AddFirst(statNode);
              }
              statNode.Add(new XElement(newFeatures.Key.ToString(),
                  new XAttribute("total", _totalFeatures[newFeatures.Key]),
                  new XAttribute("new", _totalNewFeatures[newFeatures.Key])
              ));
            }
            
          }

            

            if (_currentVersionElement.Element(Externalwikilinks) == null)
                AddExternalWikiLinks();
            _catalogDocument.Save(_catalogFile);
        }

        private void AddExternalWikiLinks()
        {
            var wiki = new XElement(Externalwikilinks);
            foreach (var item in CodeInspectionHelpers.ExternalInspectionLinks)
            {
                var link = item.Value;
                if (!link.Contains("http"))
                    link = "http://www.jetbrains.com/resharperplatform/help?Keyword=" + link;
                wiki.Add(new XElement("Item",
                    new XAttribute("Id", item.Key),
                    new XAttribute("Url", link)));
            }
            _currentVersionElement.Add(wiki);
        }

        public void AddFeatures(InspectionByLanguageGroup langGroup, RsSmallFeatureKind featureKind)
        {
            var featureRootNodeName = "Unknown";
            var featureNodeName = "Unknown";
            switch (featureKind)
            {
                case RsSmallFeatureKind.ConfigInspection:
                    featureRootNodeName = ConfigurableInspectionsRootNodeName;
                    featureNodeName = ConfigurableInspectionNodeName;
                    break;
                case RsSmallFeatureKind.StaticInspection:
                    featureRootNodeName = StaticInspectionsRootNodeName;
                    featureNodeName = StaticInspectionNodeName;
                    break;
                case RsSmallFeatureKind.ContextAction:
                    break;
                case RsSmallFeatureKind.QuickFix:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("featureKind", featureKind, null);
            }

            var totalLangFeatures = 0;
            var totalLangFeaturesInVersion = 0;

            var langElement = (from el in _currentVersionElement.Elements("lang")
                                  where (string) el.Attribute("name") == langGroup.Name
                                  select el).FirstOrDefault() ??
                              new XElement("lang", new XAttribute("name", langGroup.Name));

            if (langElement.Element(featureRootNodeName) != null)
                return;
            var featuresRootElemnt = new XElement(featureRootNodeName);

            var sortedCategories = langGroup.FeaturesByCategories[featureKind].OrderBy(o => o.Value.Name).ToList();
            foreach (var category in sortedCategories)
            {
                var catElement = (from el in langElement.Elements("category")
                                     where (string) el.Attribute("name") == category.Value.Name
                                     select el).FirstOrDefault() ??
                                 new XElement("category",
                                     new XAttribute("name", category.Value.Name));

                //var count = category.Value.ConfigurableInspections.Count;

                foreach (var feature in category.Value.Inspections)
                {
                    totalLangFeatures += 1;

                    if (!_totalFeatures.ContainsKey(featureKind)) _totalFeatures.Add(featureKind, 0);

                    _totalFeatures[featureKind] += 1;

                    if (_existingFeatures[featureKind].Contains(feature.Id)) continue;

                    catElement.Add(new XElement(featureNodeName,
                        new XAttribute("id", feature.Id),
                        new XAttribute("name", feature.Name)));
                    totalLangFeaturesInVersion += 1;

                    if (!_totalNewFeatures.ContainsKey(featureKind)) _totalNewFeatures.Add(featureKind, 0);

                    _totalNewFeatures[featureKind] += 1;
                }
                if (catElement.HasElements)
                    featuresRootElemnt.Add(catElement);
            }
            if (featuresRootElemnt.HasElements)
            {
                featuresRootElemnt.Add(new XAttribute("total", totalLangFeatures));
                featuresRootElemnt.Add(new XAttribute("new", totalLangFeaturesInVersion));
                langElement.Add(featuresRootElemnt);
            }

            if (!langElement.HasElements) return;

            if ((from el in _currentVersionElement.Elements("lang")
                    where (string) el.Attribute("name") == langGroup.Name
                    select el).FirstOrDefault() == null)
                _currentVersionElement.Add(langElement);
        }
    }
}