using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Util;
using MessageBox = System.Windows.Forms.MessageBox;

namespace RsDocGenerator
{
    public sealed class FeatureKeeper
    {
        private readonly string _catalogFile;
        private readonly XDocument _catalogDocument;
        private const string FileName = "RsFeatureCatalog.xml";
        private const string FileNameVs = "VsFeatureCatalog.xml";
        private const string RootNodeName = "FeatureCatalog";
        private const string VersionElementName = "version";
        private const string Externalwikilinks = "ExternalWikiLinks";
        private readonly XElement _currentVersionElement;

        public FeatureKeeper(IDataContext context, bool isVs = false)
        {
            var rootFolder = GeneralHelpers.GetDotnetDocsRootFolder(context);
            var currentFileName = isVs ? FileNameVs : FileName;
            
            if (rootFolder.IsNullOrEmpty()) return;

            _catalogFile = Path.Combine(rootFolder + "\\nonProject", currentFileName);
            if (File.Exists(_catalogFile))
            {
                try
                {
                    _catalogDocument = XDocument.Load(_catalogFile);
                }
                catch (Exception e)
                {
                    var result = MessageBox.Show(
                        String.Format(
                            "ReSharper feature catalog (RsFeatureCatalog.xml) is corrupted and can be neither read nor updated. \n" +
                            "Do you want to overwrite this file?"),
                        "RsFeatureCatalog.xml is corrupted", MessageBoxButtons.YesNo);
                    if (result == DialogResult.No)
                        throw;
                }
            }

            if (_catalogDocument == null)
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
            var totalFeaturesCpp = 0;

            foreach (var lang in featureCatalog.Languages)
            {
                var langPresentation = GeneralHelpers.GetPsiLanguagePresentation(lang);
                var langImplementations = featureCatalog.GetLangImplementations(lang);
                totalFeatures += langImplementations.Count;
                if (lang == "CPP")
                    totalFeaturesCpp = langImplementations.Count;

                var featureRootNodeName = featureCatalog.FeatureKind + "Node";
                var totalLangFeaturesInVersion = 0;

                var allLangFeatures = (from el in _catalogDocument.Root.Descendants("lang")
                    where (string) el.Attribute("name") == langPresentation
                    select el).Descendants(featureCatalog.FeatureKind.ToString());

                var existingLangFeatures = allLangFeatures.Select(e => e.Attribute("id").Value).ToList();
                
                var elementsWithTags = allLangFeatures.Where(e => e.Elements("tag").Any()).ToList();

                if (elementsWithTags.Any())
                {
                    foreach (var element in elementsWithTags)
                    {
                        var feature = 
                            langImplementations.FirstOrDefault(f => f.Id.Equals(element.Attribute("id").Value));
                        if (feature != null) 
                            feature.Tags = element.Elements("tag").Select(el => el.Value).ToList();
                    }
                }

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

                    if (featureCatalog.FeatureKind != RsFeatureKind.InspectionWithQuickFix)
                    {
                        featuresRootElemnt.Add(new XElement(featureCatalog.FeatureKind.ToString(),
                            new XAttribute("id", feature.Id),
                            new XAttribute("text", feature.Text)));
                    }

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

            var statFeatureNode =
                new XElement(featureCatalog.FeatureKind.ToString(),
                    new XAttribute("total", totalFeatures),
                    new XAttribute("total_no_Cpp", totalFeatures - totalFeaturesCpp));

            if (featureCatalog.FeatureKind != RsFeatureKind.InspectionWithQuickFix)
                statFeatureNode.Add(new XAttribute("new", totalFeaturesInVersion));

            statNode.Add(statFeatureNode);
        }
    }
}