using System;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;

namespace RsDocGenerator
{
    
    [Action("RsDocExportClangOptions", "Export Clang Options", Id = 195897)]
    public class RsDocExportClangOptions  : RsDocExportBase
    {
        

        protected override string GenerateContent(IDataContext context, string outputFolder)
        {
            return StartContentGeneration(context, outputFolder + "\\topics\\ReSharper\\Generated");
        }

        public static string StartContentGeneration(IDataContext context, string outputFolder)
        {
            outputFolder = outputFolder + "\\ClangOptions";
            
            var solution = context.GetData(ProjectModelDataConstants.SOLUTION);
            if (solution == null) return "Open a solution to enable generation";
            //CppClangFormatConverter.myConverters.Keys

            //var macros = solution.GetComponent<CppClangFormatConverter>();
            


            return "Clang options";
        }
    }
}