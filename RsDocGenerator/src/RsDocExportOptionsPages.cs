using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Application;
using JetBrains.Application.Catalogs;
using JetBrains.Application.Catalogs.Filtering;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.Options;
using JetBrains.Diagnostics;
using JetBrains.ReSharper.PsiGen.Util;
using JetBrains.Util.dataStructures.Sources;

namespace RsDocGenerator
{
    [ShellComponent]
    [Action("RsDocExportOptionsPages", "Export Options Pages", Id = 83373)]
    public class RsDocExportOptionsPages : RsDocExportBase
    {
        
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder);
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            SortedDictionary<string, MyOptionsPage> pages = new SortedDictionary<string, MyOptionsPage>();
            
            var productcatalogs = context.GetComponent<IPartCatalogSet>();
            var optionsPageParts = PartSelector.LeafsAndHides.SelectParts(
                    productcatalogs.Catalog.GetPartsWithAttribute<OptionsPageAttribute>().
                        ToEnumerable().OrderBy(x => x.LocalName))
                .ToList();

            foreach (var optionsPagePart in optionsPageParts)
            {
                var attributes = optionsPagePart.GetPartAttributes<OptionsPageAttribute>();

                if (attributes.Count == 0)
                    Assertion.Fail("{0} has no OptionsPageAttribute", optionsPagePart);

                if (attributes.Count > 1)
                    Assertion.Fail("{0} has {1} OptionsPageAttribute annotations. Only one annotation is supported.",
                        optionsPagePart, attributes.Count);

                var attribute = attributes.GetItemAt(0);
                var nestingType = attribute.ArgumentsOptional["NestingType"].GetBoxedValueIfDefined();

                if(nestingType is int nType && nType == 1)
                    continue;

                var id = (attribute.ArgumentsOptional["id"].GetStringValueIfDefined() ?? 
                          StringSource.Empty).ToRuntimeString();
                var parentId = (attribute.ArgumentsOptional["ParentId"].GetStringValueIfDefined() ?? 
                     StringSource.Empty).ToRuntimeString();
                var name = (attribute.ArgumentsOptional["name"].GetStringValueIfDefined() ?? 
                     StringSource.Empty).ToRuntimeString();

                pages.Add(id, new MyOptionsPage(name, id, parentId));
            }
            
            var optionsPagesLib = new HelpTopic("Options_Page_Paths", "Options pages paths", outputFolder.AddGeneratedPath());

            foreach (var optionsPage in pages.Values)
            {
                var id = optionsPage.Id.NormalizeStringForAttribute();
                var pagePath = optionsPage.Name;
                var parentPage = pages.TryGetValue(optionsPage.ParentId);

                while (parentPage != null)
                {
                    pagePath = parentPage.Name + " | " + pagePath;
                    parentPage = pages.TryGetValue(parentPage.ParentId);
                }
                
                var optionsPageChunk = XmlHelpers.CreateChunk(id);
                optionsPageChunk.Add(new XElement("ui-path", pagePath));

                var varPagePathElement = XmlHelpers.CreateVariable("page_path", pagePath);
                varPagePathElement.Add(new XAttribute("instance", "rs,dcv,dt,tca,dm"));

                var theOptionsPageChunk = XmlHelpers.CreateChunk("the_" + id + "_page");
                theOptionsPageChunk.Add(new XElement("include", 
                    new XAttribute("from", "GC.topic"),
                    new XAttribute("element-id", "the_options_page"),
                    new XAttribute("origin", "dotnet"), varPagePathElement)); 
                
                var stepOpenPageChunk = XmlHelpers.CreateChunk("step_open_" + id + "_page");
                stepOpenPageChunk.Add(new XElement("include", 
                    new XAttribute("from", "GC.topic"),
                    new XAttribute("element-id", "step_open_options_and_choose_page"),
                    new XAttribute("origin", "dotnet"), varPagePathElement));


                optionsPagesLib.Add(optionsPageChunk);
                optionsPagesLib.Add(theOptionsPageChunk);
                optionsPagesLib.Add(stepOpenPageChunk);
            }
            
            ;
            optionsPagesLib.Save();

            return "Options Pages";
        }

        
        private class MyOptionsPage
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string ParentId { get; set; }

            public MyOptionsPage(string name, string id, string parentId)
            {
                Id = id;
                Name = name;
                ParentId = parentId;
            }
        }
    }
}