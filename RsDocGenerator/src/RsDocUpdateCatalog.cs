using System;
using System.Windows.Forms;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using MessageBox = System.Windows.Forms.MessageBox;

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
            var vsFeatureKeeper = new FeatureKeeper(context, true);
            var featureDigger = new FeatureDigger(context);
            var vsFeatureDigger = new VsFeatureDigger(context);
            var configurableInspetions = featureDigger.GetConfigurableInspections();
            var vsConfigurableInspetions = vsFeatureDigger.GetConfigurableInspections();
            
            var staticInspetions = featureDigger.GetStaticInspections();
            var contextActions = featureDigger.GetContextActions();
            var quickFixes = featureDigger.GetQuickFixes();
            var vsQuickFixes = vsFeatureDigger.GetQuickFixes();
            var fixesInScope = featureDigger.GetFixesInScope();
            var actionsInScope = featureDigger.GetContextActionsInScope();
            var inspectionsWithQuickFixes = featureDigger.GetInspectionsWithFixes();

            featureKeeper.AddFeatures(configurableInspetions);
            featureKeeper.AddFeatures(staticInspetions);
            featureKeeper.AddFeatures(contextActions);
            featureKeeper.AddFeatures(quickFixes);
            featureKeeper.AddFeatures(fixesInScope);
            featureKeeper.AddFeatures(actionsInScope);
            featureKeeper.AddFeatures(inspectionsWithQuickFixes);
            
            vsFeatureKeeper.AddFeatures(vsConfigurableInspetions);
            vsFeatureKeeper.AddFeatures(vsQuickFixes);

            featureKeeper.CloseSession();
            vsFeatureKeeper.CloseSession();
            
            //RsDocUpdateVsFeaturesCatalog.Execute(context, null);

            MessageBox.Show(String.Format("ReSharper Feature Catalog (RsFeatureCatalog.xml) is updated successfully according to version {0}.",
                    GeneralHelpers.GetCurrentVersion()),
                "Export completed", MessageBoxButtons.OK);
        }
    }
}