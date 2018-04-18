using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.Diagnostics;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;
using JetBrains.Util;
using JetBrains.Util.Logging;
using JetBrains.VsIntegration.Shell;
using JetBrains.VsIntegration.Shell.Internal.Actions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace RsDocGenerator
{
 // [Action("Dump Roslyn Fixes")]
  public class RsDocUpdateVsFeaturesCatalog
    //: IExecutableAction, IInsertLast<VisualStudioInternalGroup>
  {
    public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
    {
      return true;
    }

    public static void Execute(IDataContext context, DelegateExecute nextExecute)
    {
      Dumper.DumpToNotepad(writer => Logger.Catch(() =>
      {
        try
        {
          var componentModel = context.TryGetComponent<VsComponentModelWrapper>();
          if (componentModel == null)
          {
            writer.WriteLine("Could not get IComponentModel.");
            return;
          }

          var refProviders = componentModel.GetExtensions<CodeRefactoringProvider>().ToList();
          writer.WriteLine("CodeRefactoringProvider: {0}", refProviders.Count);

          var fixProviders = componentModel.GetExtensions<CodeFixProvider>().ToList();
       
          
          writer.WriteLine("CodeFixProvider: {0}", fixProviders.Count);

          var map = new OneToListMap<string, string>();
          foreach (CodeFixProvider codeFixProvider in fixProviders)
          {
            var name = codeFixProvider.GetType().FullName;
            foreach (var id in codeFixProvider.FixableDiagnosticIds)
              map.Add(id, name);
          }
          writer.WriteLine("FixableDiagnosticIds: {0}", map.Keys.Count);

          writer.WriteLine();
          writer.WriteLine("=== Roslyn fixes by diagnostic id:");
            
          var sortedids = map.Keys.ToList();
          sortedids.Sort();
          foreach (var id in sortedids)
          {
            writer.WriteLine(id);
            foreach (var fix in map[id])
              writer.WriteLine("  {0}", fix);
          }

          writer.WriteLine();
          writer.WriteLine("=== All Roslyn fixes:"); 
          fixProviders.Sort((p1, p2) => string.CompareOrdinal(p1.GetType().FullName, p2.GetType().FullName));
          foreach (var provider in fixProviders)
          {
            var type = provider.GetType();
            writer.WriteLine("{0} ({1})", type.FullName, type.Assembly);
            foreach (var id in provider.FixableDiagnosticIds)
              writer.WriteLine("  {1}{0}", id, "ARG1");
          }

          writer.WriteLine();
          writer.WriteLine("=== All Roslyn refactorings:");
          refProviders.Sort((p1, p2) => string.CompareOrdinal(p1.GetType().FullName, p2.GetType().FullName));
          foreach (var provider in refProviders)
          {
            var type = provider.GetType();
            writer.WriteLine("{0} ({1})", type.FullName, type.Assembly);
          }
        }
        catch (Exception ex)
        {
          writer.WriteLine(ex.ToString());
        }
      }));
    }
    
        private static void GetVsConfigurableInspections()
        {
            string path = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\";

            string[] files = Directory.GetFiles(path, "*CodeAnalysis*.dll", SearchOption.AllDirectories);

            List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();

            foreach (var file in files)
            {
                var assembly = AssemblyDefinition.ReadAssembly(file);
                assemblies.Add(assembly);
            }

            var inspections = new List<string>();
            var errors = new List<string>();
            var warnings = new List<string>();
            List<string> strings = new List<string>();


            foreach (var assembly in assemblies)
            {
                using (var file = new StreamWriter(@"C:\Test\Exceptions.txt", true))
                {
                    try
                    {
                        foreach (var type in assembly.Modules.SelectMany(md => md.GetTypes()))
                        {
//                            if (type.Name.Contains("CSharpResources"))
//                            {
//                                foreach (var property in type.Properties)
//                                {
//                                    file.WriteLine("ID: " + property.GetMethod.ToString());
//                                }
//                            }


                            if (type.Name.Contains("IDEDiagnosticIds"))
                            {
                                foreach (var field in type.Fields)
                                {
                                    file.WriteLine("ID: " + field.Name + " " + field.Constant);
                                }
                            }


                            if (type.IsEnum && (type.Name.Contains("ErrorCode")|| type.Name.Contains("ERRID")))
                            {
                                foreach (var field in type.Fields)
                                {
                                    if (!errors.Contains(field.Name) && field.Name.Contains("ERR"))
                                        errors.Add(field.Name);               
                                    if (!warnings.Contains(field.Name) && field.Name.Contains("WRN"))
                                        warnings.Add(field.Name);
                                }
                            }
                            if (type.IsAbstract || type.IsInterface)
                                continue;




                            foreach (var attribute in type.CustomAttributes)
                            {
                                if (attribute.AttributeType.FullName ==
                                    "Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzerAttribute")
                                {
                         
                                    var constructors = type.GetConstructors();
                                    foreach (var constructor in constructors)
                                    {
                                        var val = constructor.Body.Instructions
                                            .Where(inst => inst.OpCode.Code == Code.Ldstr)
                                            .Select(inst => inst.Operand.ToString());
                                        foreach (var stringvalue in val)
                                        {
                                            if (stringvalue.Contains("_") || stringvalue.Contains("IDE"))
                                            {
                                                strings.Add(stringvalue);
                                                file.WriteLine("Parameter: " + stringvalue);
                                            }
                                        }
                                    }
                                    
                                    var arguments = attribute.ConstructorArguments;
                                    if(type.Name.Contains("CSharpOrderModifiersDiagnosticAnalyzer"))
                                        Console.WriteLine("test");
                                    if (!inspections.Contains(type.FullName))
                                        inspections.Add(type.FullName);
                        
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        file.WriteLine("Exception: " + assembly.FullName);
                        //Console.WriteLine("EXCEPTION:" + e);
                    }
                }
            }

            Console.WriteLine(inspections.Count);
            Console.WriteLine(errors.Count);
            Console.WriteLine(warnings.Count);
            Console.WriteLine(strings.Count);
        }

  }

}