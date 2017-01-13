using System;
using System.IO;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.UI.ActionsRevised;

namespace RsDocGenerator
{
    [Action("RsDocExportFixInScope", "Export items supporting Fix in Scope", Id = 4343)]
    internal class RsDocExportFixInScope : RsDocExportBase
    {
        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            var featureDigger = new FeatureDigger(context);
            var fixesInScope = featureDigger.GetFixesInScope();
            var actionsInScope = featureDigger.GetContextActionsInScope();

            const string caTopicId = "Fix_in_Scope_Chunks";
            var inScopeLibrary = XmlHelpers.CreateHmTopic(caTopicId);

            var qfChunk = CreateScopeChunk(fixesInScope, "qf_list");
            var caChunk = CreateScopeChunk(actionsInScope, "ca_list");

            inScopeLibrary.Root.Add(new XComment("Total quick-fix in scope: " + fixesInScope.Features.Count));
            inScopeLibrary.Root.Add(new XComment("Total context actions in scope: " + actionsInScope.Features.Count));

            inScopeLibrary.Root.Add(qfChunk);
            inScopeLibrary.Root.Add(caChunk);

            inScopeLibrary.Save(Path.Combine(outputFolder, caTopicId + ".xml"));
            return "Fix in scope actions";
        }


        private static XElement CreateScopeChunk(FeatureCatalog fixesInScope, string chunkName)
        {
            var chunk = XmlHelpers.CreateChunk(chunkName);
            foreach (var lang in fixesInScope.Languages)
            {
                var langChapter = XmlHelpers.CreateChapter(lang);
                var langList = new XElement("list");
                foreach (var fixInScope in fixesInScope.GetLangImplementations(lang))
                {
                    langList.Add(new XElement("li", fixInScope.Text + Environment.NewLine,
                        new XComment(fixInScope.Id),
                        XmlHelpers.CreateInclude("Fix_in_Scope_Static_Chunks",
                            fixInScope.Id.NormalizeStringForAttribute())));
                }
                langChapter.Add(langList);
                chunk.Add(langChapter);
            }
            return chunk;
        }
    }
}