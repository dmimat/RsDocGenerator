/*using JetBrains.Application;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.Explanatory;
using JetBrains.ReSharper.Psi.CSharp;
using RsDocGenerator;

[assembly: RegisterConfigurableSeverity(HollowTypeNameHighlighting.SeverityID, null, 
    HighlightingGroupIds.CodeSmell, "Hollow type name", "This type has a name that doesn't express its intent.",
    Severity.SUGGESTION)]

namespace RsDocGenerator
{
    [ConfigurableSeverityHighlighting(SeverityID, CSharpLanguage.Name)]
    public class HollowTypeNameHighlighting : IHighlighting
    {
        internal const string SeverityID = "HollowTypeName";
        private readonly DocumentRange documentRange;

        public HollowTypeNameHighlighting(string toolTip, DocumentRange documentRange)
        {
            ToolTip = toolTip;
            this.documentRange = documentRange;
        }

        public DocumentRange CalculateRange() => documentRange;
        public string ToolTip { get; }
        public string ErrorStripeToolTip => ToolTip;
        public bool IsValid() => true;
    }
    
    [ShellComponent]
    public class MyHelpIdProvider : ICodeInspectionWikiDataProvider
    {
        public bool TryGetValue(string attributeId, out string url)
        {
            url = "yyy";
            if (attributeId == HollowTypeNameHighlighting.SeverityID)
            {
                url = "https://www.jetbrains.com/help/resharper/sdk/HowTo/AnalyzeCode/AnalyzeCodeOnTheFly.html";
                return true;
            }

            return false;
        }
    }
}*/

