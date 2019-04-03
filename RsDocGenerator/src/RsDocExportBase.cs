using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;

namespace RsDocGenerator
{
    public abstract class RsDocExportBase : IExecutableAction
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            var outputFolder = GeneralHelpers.GetDotnetDocsRootFolder(context);
            if (string.IsNullOrEmpty(outputFolder)) return;
            var what = GenerateContent(context, outputFolder);
            GeneralHelpers.ShowSuccessMessage(what, outputFolder);
        }

        public abstract string GenerateContent(IDataContext context, string outputFolder);
    }
}