using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.UI.ActionsRevised;
using JetBrains.Util;

namespace RsDocGenerator
{
    [Action("RsDocUpdateAll", "Update All Generated Topics", Id = 8473437)]
    internal class RsDocUpdateAll : IExecutableAction
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            var outputFolder = GeneralHelpers.GetDotnetDocsRootFolder(context);
            if (outputFolder.IsNullOrEmpty()) return;

            RsDocExportShortcuts.StartContentGeneration(context,outputFolder);
            RsDocUpdateCatalog.UpdateCatalog(context);

            var generatedFolder = outputFolder + "\\topics\\ReSharper\\Generated";

            RsDocExportTemplates.StartContentGeneration(context,generatedFolder);
            RsDocExportPostfixTemplates.StartContentGeneration(context,generatedFolder);
            RsDocExportMacros.StartContentGeneration(context,generatedFolder);
            RsDocExportInspectionsIndex.StartContentGeneration(context,generatedFolder);
            RsDocExportContextActions.StartContentGeneration(context,generatedFolder);
            RsDocExportFixInScope.StartContentGeneration(context,generatedFolder);
            RsDocExportThirdParty.StartContentGeneration(context,generatedFolder);

            GeneralHelpers.ShowSuccessMessage("Everything", outputFolder);
        }
    }
}
