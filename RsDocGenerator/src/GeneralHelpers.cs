using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;
using MessageBox = System.Windows.Forms.MessageBox;

namespace RsDocGenerator
{
    internal static class GeneralHelpers
    {
        private static readonly Dictionary<string, string> PsiLangugages = new Dictionary<string, string>
        {
            {"CSHARP", "C#"},
            {"CPP", "C++"},
            {"VBASIC", "VB.NET"},
            {"ASPX", "ASP.NET"},
            {"JAVA_SCRIPT", "JavaScript"},
            {"XAML", "XAML"},
            {"RESX", "Resource files"},
            {"HTML", "HTML"},
            {"CSS", "CSS"},
            {"Web.Config", "Web.config"},
            {"MSBUILD_BUILD_SCRIPT", "MSBuild"},
            {"NANT_BUILD_SCRIPT", "NAnt"},
            {"BUILD_SCRIPT", "Build Scripts"},
            {"ASXX", "HttpHandler or WebService"},
            {"Razor", "Razor"},
            {"TYPE_SCRIPT", "TypeScript"},
            {"REGULAR_EXPRESSION", "Regular expressions"}
        };

        public static string GetCurrentVersion()
        {
            var subProducts = Shell.Instance.GetComponent<SubProducts>();
            foreach (var subProduct in subProducts.SubProductsInfos)
                if (subProduct.ProductPresentableName.Contains("ReSharper"))
                    return subProduct.VersionMarketingString;
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

        public static string TryGetPsiLangFromTypeName(string fullName)
        {
            if (fullName.Contains("Asp"))
                return "ASPX";
            if (fullName.Contains("CSharp"))
                return "CSHARP";
            if (fullName.Contains("VB"))
                return "VBASIC";
            if (fullName.Contains("TypeScript"))
                return "TYPE_SCRIPT";
            if (fullName.Contains("JavaScript"))
                return "JAVA_SCRIPT";
            if (fullName.Contains("Html"))
                return "HTML";
            if (fullName.Contains("BuildScripts"))
                return "Build scripts";
            if (fullName.Contains("Xaml"))
                return "XAML";
            if (fullName.Contains("Razor"))
                return "Razor";
            if (fullName.Contains("Css"))
                return "CSS";
            if (fullName.Contains("WebConfig"))
                return "Web.Config";
            if (fullName.Contains("Xml"))
                return "XML";
            if (fullName.Contains("Resx"))
                return "RESX";
            if (fullName.Contains("RegExp"))
                return "REGULAR_EXPRESSION";
            if (fullName.Contains("Cpp"))
                return "CPP";
            return "Common";
        }
        
        public static bool IsLangSupportedInRider(this string lang)
        {
            switch (lang)
            {
                case "C++" :
                case "CPP" :
                case "JavaScript":
                case "TypeScript":
                case "HTML":
                case "XML":
                case "XMLDOC":
                case "Css":
                case "CSS":
                case "Protobuf":
                    return false;
            }
            return true;
        }

        [NotNull]
        [Pure]
        public static string GetPsiLanguagePresentation(string psiLangId)
        {
            return PsiLangugages.ContainsKey(psiLangId) ? PsiLangugages[psiLangId] : psiLangId;
        }

        public static string GetPsiLangByPresentation(string lang)
        {
            if (lang == null || lang == "General") return "Common";
            if (lang == "Cpp") return "CPP";
            if (lang == "VB") return "VBASIC";
            foreach (var psiLangugage in PsiLangugages)
                if (lang == psiLangugage.Value)
                    return psiLangugage.Key;
            return lang;
        }

        [CanBeNull]
        public static string GetDotnetDocsRootFolder(IDataContext context)
        {
            return GetPathFromSettings(context, key => key.RsDocDotnetRootFolder,
                "Choose the dotnet help directory root");
        }

   
        public static string GetGeneratedDocsFolder(this string outputFolder)
        {
            return outputFolder + "\\topics\\ReSharper\\Generated";
        }


        [CanBeNull]
        public static string GetCaPath(IDataContext context)
        {
            return GetPathFromSettings(context, key => key.RsDocCaFolder,
                "Specify the folder with context actions samples");
        }

        [CanBeNull]
        private static string GetPathFromSettings(IDataContext context,
            Expression<Func<RsDocSettingsKey, string>> expression, string description)
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
            var result = MessageBox.Show(string.Format("{0} are saved successfully to \n" +
                                                       "{1}\n" +
                                                       "Do you want to open the output folder?", what, where),
                "Export completed", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes) Process.Start(where);
        }

        public static string TextFromTypeName(this string input)
        {
            input = input.TrimFromEnd("Error");
            input = input.TrimFromEnd("QuickFix");
            input = input.TrimFromEnd("Fix");

            var substringsToReplace = new List<string> {"Cpp", "Asp", "Xaml", "Razor", "Html", "Css", "CSharp"};
            input = substringsToReplace.Aggregate(input, (current, substr) => current.TrimFromStart(substr));

            if (input.IsNullOrEmpty())
                return string.Empty;

            var splitString = Regex.Replace(input, "([A-Z])", " $1",
                RegexOptions.Compiled).Trim();
            var name = splitString.Substring(0, 1) + splitString.Substring(1).ToLower();
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static string CleanProductName(this string input)
        {
            if (input == null) return input;
            input = input.Replace(" ReSharper ", " %product% ");
            input = input.Replace(" options", " %settingsLower%");
            return input;
        }
    }
}