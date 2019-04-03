using System.Windows.Forms;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;

namespace RsDocGenerator
{
    [Action("RsDocUpdateCatalog", "Update ReSharper Feature Catalog (RsFeatureCatalog.xml)", Id = 43292)]
    internal class RsDocUpdateCatalog : IExecutableAction
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            UpdateCatalog(context);
        }

        public static void UpdateCatalog(IDataContext context)
        {
            var featureKeeper = new FeatureKeeper(context);
            var featureDigger = new FeatureDigger(context);
            var vsFeatureKeeper = new FeatureKeeper(context, true);
//            var vsFeatureDigger = new VsFeatureDigger(context);

            var configurableInspections = featureDigger.GetConfigurableInspections();
            var staticInspections = featureDigger.GetStaticInspections();
            var contextActions = featureDigger.GetContextActions();
            var quickFixes = featureDigger.GetQuickFixes();
            var fixesInScope = featureDigger.GetFixesInScope();
            var actionsInScope = featureDigger.GetContextActionsInScope();
            var inspectionsWithQuickFixes = featureDigger.GetInspectionsWithFixes();

//            var vsQuickFixes = vsFeatureDigger.GetQuickFixes();
//            var vsConfigurableInspections = vsFeatureDigger.GetConfigurableInspections();
//            var vsStaticInspections = vsFeatureDigger.GetStaticInspections();

            featureKeeper.AddFeatures(configurableInspections);
            featureKeeper.AddFeatures(staticInspections);
            featureKeeper.AddFeatures(contextActions);
            featureKeeper.AddFeatures(quickFixes);
            featureKeeper.AddFeatures(fixesInScope);
            featureKeeper.AddFeatures(actionsInScope);
            featureKeeper.AddFeatures(inspectionsWithQuickFixes);

//            vsFeatureKeeper.AddFeatures(vsConfigurableInspections);
//            vsFeatureKeeper.AddFeatures(vsQuickFixes);
//            vsFeatureKeeper.AddFeatures(vsStaticInspections);

            featureKeeper.CloseSession();
            vsFeatureKeeper.CloseSession();

            var featuresByTag = new TagKeeper(context);
            featuresByTag.AddFeatures(configurableInspections, "ReSharper");
//            featuresByTag.AddFeatures(vsConfigurableInspections, "Visual Studio");

            featuresByTag.CloseSession();

            RsDocUpdateVsFeaturesCatalog.Execute(context, null);

            MessageBox.Show(string.Format(
                    "ReSharper Feature Catalog (RsFeatureCatalog.xml) is updated successfully according to version {0}.",
                    GeneralHelpers.GetCurrentVersion()),
                "Export completed", MessageBoxButtons.OK);
        }
    }
}