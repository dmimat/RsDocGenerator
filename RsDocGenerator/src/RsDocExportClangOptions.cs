using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.Cpp.CodeStyle;

namespace RsDocGenerator
{
    [Action("RsDocExportClangOptions", "Export Clang Options", Id = 195897)]
    public class RsDocExportClangOptions : RsDocExportBase
    {
        public override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder.AddGeneratedPath());
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            outputFolder = outputFolder + "\\ClangOptions";

            var solution = context.GetData(ProjectModelDataConstants.SOLUTION);
            if (solution == null) return "Open a solution to enable generation";
            //CppClangFormatConverter.myConverters.Keys

            var clangFormatConverter = solution.GetComponent<CppClangFormatConverter>();

            return "Clang options";
        }
    }
}