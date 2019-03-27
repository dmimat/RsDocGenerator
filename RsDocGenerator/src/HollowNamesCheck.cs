/*using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace RsDocGenerator
{
    [ElementProblemAnalyzer(typeof(IClassDeclaration),
        HighlightingTypes = new []
        {
            typeof(HollowTypeNameHighlighting)
        })]
    public class HollowNamesCheck : ElementProblemAnalyzer<IClassDeclaration>
    {
        protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var suffixes = GetSuffixes(data.SettingsStore);

            var match = GetFirstMatchOrDefault(element.DeclaredName, suffixes);
            if (match != null)
                AddHighlighting(match, consumer, element);
        }

        private IEnumerable<string> GetSuffixes(IContextBoundSettingsStore dataSettingsStore)
        {
            var suffixes = "crap, stuff";
            return suffixes.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetFirstMatchOrDefault(string declaredName, IEnumerable<string> suffixes)
        {
            return suffixes.FirstOrDefault(declaredName.EndsWith);
        }

        private void AddHighlighting(string bannedSuffix, IHighlightingConsumer consumer, IClassDeclaration typeExpression)
        {
            var identifier = typeExpression.NameIdentifier;
            var documentRange = identifier.GetDocumentRange();
            var highlighting = new HollowTypeNameHighlighting("This is my tooltip", documentRange);
            consumer.AddHighlighting(highlighting);
        }
    }
}*/

