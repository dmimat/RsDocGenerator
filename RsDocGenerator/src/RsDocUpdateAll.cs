using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
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

            RsDocExportShortcuts.StartContentGeneration(context, outputFolder);
            RsDocExportOptionsPages.StartContentGeneration(context, outputFolder);
            RsDocExportOptions.StartContentGeneration(context, outputFolder);
            RsDocUpdateCatalog.UpdateCatalog(context);
            
            RsDocExportTemplates.StartContentGeneration(context, outputFolder);
            RsDocExportPostfixTemplates.StartContentGeneration(context, outputFolder);
            RsDocExportMacros.StartContentGeneration(context, outputFolder);
            RsDocExportInspectionsIndex.StartContentGeneration(context, outputFolder);
            RsDocExportContextActions.StartContentGeneration(context, outputFolder);
            RsDocExportFixInScope.StartContentGeneration(context, outputFolder);
            //RsDocExportThirdParty.StartContentGeneration(context, outputFolder);
            RsDocExportEditorConfigStyles.StartContentGeneration(context, outputFolder);


            GeneralHelpers.ShowSuccessMessage("Everything", outputFolder);
        }
    }
}