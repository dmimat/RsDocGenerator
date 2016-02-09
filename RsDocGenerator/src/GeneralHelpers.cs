using System.Text.RegularExpressions;
using JetBrains.Application;
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
    }
}
