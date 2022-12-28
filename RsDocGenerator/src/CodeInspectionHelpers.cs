using System.Collections.Generic;

namespace RsDocGenerator
{
    public static class CodeInspectionHelpers
    {
        static CodeInspectionHelpers()
        {
            ExternalInspectionLinks = new Dictionary<string, string>
            {
                {"CSharpWarnings::CS0108", "https://msdn.microsoft.com/en-us/library/3s8070fc.aspx"},
                {"CSharpWarnings::CS0109", "https://msdn.microsoft.com/en-us/library/css4y2c4.aspx"},
                {"CSharpWarnings::CS0162", "https://msdn.microsoft.com/en-us/library/c0h4st1x.aspx"},
                {"CSharpWarnings::CS0183", "https://msdn.microsoft.com/en-us/library/sb7782xb.aspx"},
                {"CSharpWarnings::CS0184", "https://msdn.microsoft.com/en-us/library/230kb9yt.aspx"},
                {"CSharpWarnings::CS0197", "https://msdn.microsoft.com/en-us/library/y545659k.aspx"},
                {"CSharpWarnings::CS0252", "https://msdn.microsoft.com/en-us/library/f6dtw2ah.aspx"},
                {"CSharpWarnings::CS0420", "https://msdn.microsoft.com/en-us/library/4bw5ewxy.aspx"},
                {"CSharpWarnings::CS0465", "https://msdn.microsoft.com/en-us/library/02wtfwbt.aspx"},
                {"CSharpWarnings::CS0469", "https://msdn.microsoft.com/en-us/library/ms228370.aspx"},
                {"CSharpWarnings::CS0612", "https://msdn.microsoft.com/en-us/library/h0h063ka.aspx"},
                {"CSharpWarnings::CS0618", "https://msdn.microsoft.com/en-us/library/x5ye6x1e.aspx"},
                {"CSharpWarnings::CS0628", "https://msdn.microsoft.com/en-us/library/7x8ekes3.aspx"},
                {"CSharpWarnings::CS0642", "https://msdn.microsoft.com/en-us/library/9x19t380.aspx"},
                {"CSharpWarnings::CS0657", "https://msdn.microsoft.com/en-us/library/c6hdfbk4.aspx"},
                {"CSharpWarnings::CS0658", "https://msdn.microsoft.com/en-us/library/4ky08ezz.aspx"},
                {"CSharpWarnings::CS0659", "https://msdn.microsoft.com/en-us/library/xxhbfytk.aspx"},
                {"CSharpWarnings::CS0660", "https://msdn.microsoft.com/en-us/library/4wtxwb6k.aspx"},
                {"CSharpWarnings::CS0665", "https://msdn.microsoft.com/en-us/library/c1sde1ax.aspx"},
                {"CSharpWarnings::CS0672", "https://msdn.microsoft.com/en-us/library/9dzeyth8.aspx"},
                {"CSharpWarnings::CS0693", "https://msdn.microsoft.com/en-us/library/0ah54ze5.aspx"},
                {"CSharpWarnings::CS1030", "https://msdn.microsoft.com/en-us/library/ckcykyd4.aspx"},
                {"CSharpWarnings::CS1058", "https://msdn.microsoft.com/en-us/library/ms228623.aspx"},
                {"CSharpWarnings::CS1522", "https://msdn.microsoft.com/en-us/library/x68b4s45.aspx"},
                {"CSharpWarnings::CS1570", "https://msdn.microsoft.com/en-us/library/c20zzdxx.aspx"},
                {"CSharpWarnings::CS1571", "https://msdn.microsoft.com/en-us/library/a5c6cbk0.aspx"},
                {"CSharpWarnings::CS1573", "https://msdn.microsoft.com/en-us/library/01248w2b.aspx"},
                {"CSharpWarnings::CS1574", "https://msdn.microsoft.com/en-us/library/26x4hk2a.aspx"},
                {"CSharpWarnings::CS1580", "https://msdn.microsoft.com/en-us/library/03t96cfx.aspx"},
                {"CSharpWarnings::CS1584", "https://msdn.microsoft.com/en-us/library/hz13h4se.aspx"},
                {"CSharpWarnings::CS1587", "https://msdn.microsoft.com/en-us/library/d3x6ez1z.aspx"},
                {"CSharpWarnings::CS1589", "https://msdn.microsoft.com/en-us/library/3y857kz5.aspx"},
                {"CSharpWarnings::CS1590", "https://msdn.microsoft.com/en-us/library/549c3y6s.aspx"},
                {"CSharpWarnings::CS1591", "https://msdn.microsoft.com/en-us/library/zk18c1w9.aspx"},
                {"CSharpWarnings::CS1592", "https://msdn.microsoft.com/en-us/library/89c331t3.aspx"},
                {"CSharpWarnings::CS1710", "https://msdn.microsoft.com/en-us/library/k5ya7w1x.aspx"},
                {"CSharpWarnings::CS1712", "https://msdn.microsoft.com/en-us/library/t8zca749.aspx"},
                {"CSharpWarnings::CS1717", "https://msdn.microsoft.com/en-us/library/a1kzfw0z.aspx"},
                {"CSharpWarnings::CS1723", "https://msdn.microsoft.com/en-us/library/ms228603.aspx"},
                {"CSharpWarnings::CS1911", "https://msdn.microsoft.com/en-us/library/ms228459.aspx"},
                {"CSharpWarnings::CS1957", "https://msdn.microsoft.com/en-us/library/bb882562.aspx"},
                {"CSharpWarnings::CS4014", "https://msdn.microsoft.com/en-us/library/hh873131.aspx"},
                {"CSharpWarnings::CS0078", "https://msdn.microsoft.com/en-us/library/s74dtt7k.aspx"},
                {
                    "CSharpWarnings::CS1584,CS1711,CS1572,CS1581,CS1580",
                    "CSharpWarnings_CS1584_CS1711_CS1572_CS1581_CS1580"
                },
                {"CSharpWarnings::CS0108,CS0114", "CSharpWarnings_CS0108_CS0114"},
                {"CSharpWarnings::CS0660,CS0661", "CSharpWarnings_CS0660_CS0661"},
                {"CSharpWarnings::CS0252,CS0253", "CSharpWarnings_CS0252_CS0253"},
                {"VBWarnings::BC42105,BC42106,BC42107", "VBWarnings_BC42105_BC42106_BC42107"},
                {"VBWarnings::BC42353,BC42354,BC42355", "VBWarnings_BC42353_BC42354_BC42355"},
                {"VBWarnings::BC400005", "https://msdn.microsoft.com/en-us/library/fs06ef5d.aspx"},
                {"VBWarnings::BC42358", "https://msdn.microsoft.com/en-us/library/hh965065.aspx"},
                {"VBWarnings::BC42025", "https://msdn.microsoft.com/en-us/library/y6t76186.aspx"},
                {"VBWarnings::BC40056", "https://msdn.microsoft.com/en-us/library/ms234657.aspx"},
                {"VBWarnings::BC42016", "https://msdn.microsoft.com/en-us/library/56k670kt.aspx"},
                {"VBWarnings::BC40008", "https://msdn.microsoft.com/en-us/library/s5f0ewa6.aspx"},
                {"VBWarnings::BC42104", "https://msdn.microsoft.com/en-us/library/3fdk625a.aspx"},
                {"VBWarnings::BC42304", "https://docs.microsoft.com/en-us/dotnet/visual-basic/misc/bc42304"},
                {"VBWarnings::BC42309", "https://docs.microsoft.com/en-us/dotnet/visual-basic/misc/bc42309"},
                {"VBWarnings::BC42322", "https://docs.microsoft.com/en-us/dotnet/visual-basic/misc/bc42322"}
            };

            PsiLanguagesByCategoryNames = new Dictionary<string, string>
            {
                {"CSharpErrors", "CSHARP"},
                {"WebConfig Errors", "Web.Config"},
                {"XAML Errors", "XAML"},
                {"HtmlErrors", "HTML"},
                {"XMLErrors", "XML"},
                {"AsxxErrors", "ASXX"},
                {"AspErrors", "ASPX"},
                {"CppCompilerErrors", "CPP"},
                {"VBErrors", "VBASIC"},
                {"JScriptErrors", "JAVA_SCRIPT"},
                {"Razor Errors", "Razor"},
                {"Razor Warnings", "Razor"},
                {"Razor Info", "Razor"},
            };
        }

        public static Dictionary<string, string> ExternalInspectionLinks { get; set; }
        public static Dictionary<string, string> PsiLanguagesByCategoryNames { get; set; }

        public static string TryGetStaticHref(string inspectionId)
        {
            if (ExternalInspectionLinks.ContainsKey(inspectionId))
                return ExternalInspectionLinks[inspectionId];
            if (inspectionId.Contains("::"))
                return "NO_LINK";
            return inspectionId;
        }
    }
}