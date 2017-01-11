using System;
using System.IO;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.UI.ActionsRevised;
using System.Diagnostics;
using System.Windows.Forms;
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
            var featureKeeper = new FeatureKeeper(context);
            var featureDigger = new FeatureDigger(context);
            var configurableInspetions = featureDigger.GetConfigurableInspections();
            var staticInspetions = featureDigger.GetStaticInspections();
            var contextActions = featureDigger.GetContextActions();
            var quickFixes = featureDigger.GetQuickFixes();
            var fixesInScope = featureDigger.GetFixesInScope();
            var actionsInScope = featureDigger.GetContextActionsInScope();

            featureKeeper.AddFeatures(configurableInspetions);
            featureKeeper.AddFeatures(staticInspetions);
            featureKeeper.AddFeatures(contextActions);
            featureKeeper.AddFeatures(quickFixes);
            featureKeeper.AddFeatures(fixesInScope);
            featureKeeper.AddFeatures(actionsInScope);

            featureKeeper.CloseSession();

            var result = MessageBox.Show(String.Format("ReSharper Feature Catalog (RsFeatureCatalog.xml) is updated successfully according to version {0}. \n" +
                                          "Do you want to open its location?", GeneralHelpers.GetCurrentVersion()),
                "Export completed", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes) { Process.Start(GeneralHelpers.GetFeatureCatalogFolder(context) ?? Path.GetTempPath()); }
        }

    }
}