using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Util;

namespace RsDocGenerator
{
    public sealed class TagKeeper
    {
        private const string FileName = "FeaturesByTags.xml";
        private readonly XDocument _catalogDocument;
        private readonly string _catalogFile;
        private readonly XElement _catalogRoot = new XElement("Tags");

        public TagKeeper(IDataContext context)
        {
            var rootFolder = GeneralHelpers.GetDotnetDocsRootFolder(context);

            if (rootFolder.IsNullOrEmpty()) return;

            _catalogFile = Path.Combine(rootFolder + "\\nonProject", FileName);
            _catalogDocument = new XDocument();
            _catalogDocument.Add(new XComment("C++ features are not included"));

            var currentVersionString = GeneralHelpers.GetCurrentVersion();

            _catalogRoot.Add(new XElement("Other"));
        }

        public void CloseSession()
        {
            _catalogDocument.Add(_catalogRoot);
            _catalogDocument.Save(_catalogFile);
        }

        private void AddElementByTag(RsFeature feature, string tag, string product)
        {
            var tagElement = _catalogRoot.Elements(tag).FirstOrDefault();
            if (tagElement == null)
            {
                tagElement = new XElement(tag);
                _catalogRoot.Add(tagElement);
            }

            var featureTypeElement = tagElement.Elements(feature.Kind + "Node").FirstOrDefault();
            if (featureTypeElement == null)
            {
                featureTypeElement = new XElement(feature.Kind + "Node");
                tagElement.Add(featureTypeElement);
            }

            featureTypeElement.Add(new XElement(feature.Kind.ToString(),
                new XAttribute("lang", GeneralHelpers.GetPsiLanguagePresentation(feature.Lang)),
                new XAttribute("product", product),
                new XAttribute("id", feature.Id),
                new XAttribute("text", feature.Text)));
        }

        public void AddFeatures(FeatureCatalog featureCatalog, string product)
        {
            foreach (var feature in featureCatalog.Features)
            {
                if (feature.Lang == "CPP")
                    continue;
                if (feature.Tags.Any())
                    foreach (var tag in feature.Tags)
                        AddElementByTag(feature, tag, product);
                else
                    AddElementByTag(feature, "Other", product);
            }
        }
    }
}