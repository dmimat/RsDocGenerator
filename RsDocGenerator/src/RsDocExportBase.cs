using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;
using JetBrains.UI.ActionsRevised;

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

    protected abstract string GenerateContent(IDataContext context, string outputFolder);
  }
}
