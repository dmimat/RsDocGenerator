using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Resources.Shell;

namespace RsDocGenerator
{
  static class GeneralHelpers
  {
    public static string GetCurrentVersion()
    {
      var subProducts = Shell.Instance.GetComponent<SubProducts>();
      foreach (var subProduct in subProducts.SubProductsInfos)
      {
        if (subProduct.ProductPresentableName.Contains("ReSharper"))
          return subProduct.VersionMarketingString;
      }
      return "unknown";
    }

    public static string NormalizeStringForAttribute(this string value)
    {
      value = value.Replace("#", "SHARP").Replace("++", "PP");
      return Regex.Replace(value, @"[\.\s-]", "_");
    }

    public static string CleanCodeSample(this string value)
    {
      return value.Replace("{caret}", "").Replace("{selstart}", "").Replace("{selend}", "");
    }

    public static string TryGetLang(string fullName)
    {
      if (fullName.Contains("Asp.CSharp"))
        return "ASP.NET (C#)";
      if (fullName.Contains("Asp.VB"))
        return "ASP.NET (VB.NET)";
      if (fullName.Contains("Asp"))
        return "ASP.NET";
      if (fullName.Contains("CSharp"))
        return "C#";
      if (fullName.Contains("VB"))
        return "VB.NET";
      if (fullName.Contains("TypeScript"))
        return "TypeScript";
      if (fullName.Contains("JavaScript"))
        return "JavaScript";
      if (fullName.Contains("Html"))
        return "HTML";
      if (fullName.Contains("Cpp"))
        return "C++";
      return "Common";
    }


      [NotNull, Pure]
      public static string GetPsiLanguagePresentation([NotNull] PsiLanguageType type)
      {
          switch (type.Name)
          {
              case "VBASIC":
                  return "VB.NET";
              case "ASPX":
                  return "ASP.NET";
              case "JAVA_SCRIPT":
                  return "JavaScript";
              case "XAML":
                  return "XAML";
              case "RESX":
                  return "Resource files";
              case "HTML":
                  return "HTML";
              case "CSS":
                  return "CSS";
              case "Web.Config":
                  return "Web.config";
              case "MSBUILD_BUILD_SCRIPT":
                  return "MSBuild";
              case "NANT_BUILD_SCRIPT":
                  return "NAnt";
              case "ASXX":
                  return "HttpHandler or WebService";
              case "Razor":
                  return "Razor";
              case "TYPE_SCRIPT":
                  return "TypeScript";
              case "REGULAR_EXPRESSION":
                  return "Regular expressions";
              default:
                  return type.PresentableName;
          }
      }

      [CanBeNull]
    public static string GetOutputFolder(IDataContext context)
    {
      return GetPathFromSettings(context, key => key.RsDocOutputFolder, "Choose where to save XML topics");
    }

    [CanBeNull]
    public static string GetCaPath(IDataContext context)
    {
      return GetPathFromSettings(context, key => key.RsDocCaFolder, "Specify the folder with context actions samples");
    }

      [CanBeNull]
    public static string GetFeatureCatalogFolder(IDataContext context)
    {
      return GetPathFromSettings(context, key => key.RsDocFeatureCatalog, "Specify the folder for feature catalog file (RsFeatureCatalog.xml)");
    }

    [CanBeNull]
    private static string GetPathFromSettings(IDataContext context, Expression<Func<RsDocSettingsKey, string>> expression, string description)
    {
      var contextBoundSettingsStore =
        context.GetComponent<ISettingsStore>().BindToContextTransient(ContextRange.ApplicationWide);

      var path = contextBoundSettingsStore.GetValue(expression);
        if (!string.IsNullOrEmpty(path)) return path;
        using (var brwsr = new FolderBrowserDialog {Description = description})
        {
            if (brwsr.ShowDialog() == DialogResult.Cancel) return null;
            path = brwsr.SelectedPath;
            contextBoundSettingsStore.SetValue(expression, path);
        }
        return path;
    }


    public static void ShowSuccessMessage(string what, string where)
    {
      DialogResult result = MessageBox.Show(String.Format("{0} are saved successfully to \n" +
                                                          "{1}\n" +
                                                          "Do you want to open the output folder?", what, where),
        "Export completed", MessageBoxButtons.YesNo);
      if (result == DialogResult.Yes) { Process.Start(where); }
    }

    public static string RemoveFromEnd(this string s, string suffix)
    {
      if (s.EndsWith(suffix))
      {
        return s.Substring(0, s.Length - suffix.Length);
      }
      return s;
    }
  }
}
