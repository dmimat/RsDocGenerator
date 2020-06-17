using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Feature.Services.QuickFixes;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;
using MessageBox = JetBrains.Util.MessageBox;

namespace RsDocGenerator
{
    // This is an old implementation, doesn't work now, but may contain some hints as of where to get the data from.

    [Action("RsDocExportFeatures", Id = 6969)]
    internal class RsDocExportFeatures : IExecutableAction
    {
        private readonly string[] myTestAssemblyNames =
        {
            "JetBrains.ReSharper.IntentionsTests",
            "JetBrains.ReSharper.Intentions.Asp.Tests",
            "JetBrains.ReSharper.Intentions.Asp.CSharp.Tests",
            "JetBrains.ReSharper.Intentions.Asp.VB.Tests",
            "JetBrains.ReSharper.Intentions.Html.Tests",
            "JetBrains.ReSharper.Intentions.JavaScript.Tests",
            "JetBrains.ReSharper.Intentions.Razor.Tests",
            "JetBrains.ReSharper.Intentions.VB.Tests",
            "JetBrains.ReSharper.AspTests",
            "JetBrains.ReSharper.BuildScriptTests",
            "JetBrains.ReSharper.CssTests",
            "JetBrains.ReSharper.HtmlTests",
            "JetBrains.ReSharper.JavaScriptTests"
        };

        private readonly List<Type> myUnmatchedTests = new List<Type>();

        private List<Assembly> myTestAssemblies;
        private string version = "undefined";

        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            ExportFeaturesForDocumentation();

//            using (var brwsr = new FolderBrowserDialog() { Description = "Choose where to save XML topics." })
//            {
//                if (brwsr.ShowDialog() == DialogResult.Cancel) return;
//
//     
//
////                caLibrary.Save(Path.Combine(saveDirectoryPath, caTopicId + ".xml"));
//                MessageBox.ShowInfo("Context Actions exported successfully");
//
//            }
        }

        /// <summary>
        ///     Export all features for documentation
        /// </summary>
        private void ExportFeaturesForDocumentation()
        {
            MessageBox.ShowInfo("latest");
            var brwsr = new FolderBrowserDialog {Description = "Choose where to save the ResharperFeatures.xml file."};
            if (brwsr.ShowDialog() == DialogResult.Cancel) return;
            var saveDirectoryPath = brwsr.SelectedPath;
            var fileName = Path.Combine(saveDirectoryPath, "ResharperFeatures.xml");
            var xDoc = new XDocument();
            var xDocRoot = new XElement("Features");

//            var productVersion = new ReSharperApplicationDescriptor().ProductVersion;
//            version = String.Format("{0}.{1}", productVersion.Major, productVersion.Minor);
            version = "9.0";

            myTestAssemblies = new List<Assembly>();
            foreach (var testAssemblyName in myTestAssemblyNames)
            {
                var testAssembly = Assembly.Load(testAssemblyName);
                myTestAssemblies.Add(testAssembly);
            }

            // var cache = this.GetTestsForFeatures();

            // Context actions
            var cAnumber = 0;
//            var contextActionTable = Shell.Instance.GetComponent<ContextActionTable>();
//            foreach (var ca in contextActionTable.AllActions)
//            {
//                AddFeature(xDocRoot, ca.MergeKey.Split('.').Last(), ca.Type,
//                  "ContextAction", version, ca.Name, ca.Description, String.Empty, cache, null);
//                cAnumber++;
//            }

            // Quick fixes and Inspections
            var qFnumber = 0;
            var insTypeNumber = 0;
            var quickFixTable = Shell.Instance.GetComponent<QuickFixTable>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = null;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception e)
                {
                    MessageBox.ShowError("Cannot load assembly!!!!" + e);
                    continue;
                }

                if (types == null) continue;

                foreach (var type in types)
                {
                    var name = "Unknown";
                    var description = "Unknown";
                    var lang = string.Empty;
                    var attrs = Attribute.GetCustomAttributes(type);

                    // Quick fixes
                    if (typeof(IQuickFix).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract) qFnumber++;

                    // Inspections
                    if (typeof(IHighlighting).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var id = string.Empty;
                        var severity = string.Empty;
                        var group = string.Empty;
                        var solutionWide = string.Empty;
                        var allQuickFixes = string.Empty;
                        var configurable = string.Empty;
                        var compoundName = string.Empty;
                        /// TODO: Make GetHighlightingQuickFixesInfo public
//                        var quickFixes = quickFixTable.GetHighlightingQuickFixesInfo(type);

//                        foreach (var quickFix in quickFixes)
//                            allQuickFixes += quickFix.QuickFixType.Name + ",";

                        if (!string.IsNullOrEmpty(allQuickFixes))
                            allQuickFixes = allQuickFixes.TrimEnd(',');

                        foreach (var attr in attrs)
                        {
                            if (attr is ConfigurableSeverityHighlightingAttribute)
                            {
                                var a = (ConfigurableSeverityHighlightingAttribute) attr;
                                id = a.ConfigurableSeverityId;
                                var inspectionInstance = HighlightingSettingsManager.Instance.TryGetSeverityItem(id);
                                lang = GetLangsForInspection(id);
                                if (inspectionInstance != null)
                                {
                                    name = inspectionInstance.FullTitle;
                                    description = inspectionInstance.Description;
                                    severity = inspectionInstance.DefaultSeverity.ToString();
                                    solutionWide = inspectionInstance.SolutionAnalysisRequired ? "yes" : "no";
                                    group = inspectionInstance.GroupId;
                                    compoundName = inspectionInstance.CompoundItemName;
                                    configurable = "yes";
                                    break;
                                }
                            }

                            if (attr is StaticSeverityHighlightingAttribute)
                            {
                                id = type.Name;
                                var a = (StaticSeverityHighlightingAttribute) attr;
                                name = a.ToolTipFormatString;
                                group = a.GroupId;
                                severity = a.Severity.ToString();
                                solutionWide = "no";
                                configurable = "no";
                            }
                        }

                        if (name != "Unknown")
                        {
                            var details = new XElement("Details",
                                new XAttribute("DefaultSeverity", severity),
                                new XAttribute("Group", group),
                                new XAttribute("SolutionWide", solutionWide),
                                new XAttribute("Configurable", configurable),
                                new XAttribute("CompoundName", compoundName ?? ""),
                                new XAttribute("QuickFixes", allQuickFixes)
                            );
                            if (string.IsNullOrEmpty(name) && description == "Unknown")
                                name = SplitCamelCase(type.Name);
                            if (string.IsNullOrEmpty(name)) name = description;
                            //this.AddFeature(xDocRoot, id, type, "Inspection", this.version, name, description, lang, cache, details);
                            insTypeNumber++;
                        }
                    }
                }
            }

            xDocRoot.Add(new XAttribute("TotalContextActions", cAnumber),
                new XAttribute("TotalQuickFixes", qFnumber),
                new XAttribute("TotalInspections", insTypeNumber));

            foreach (var ins in HighlightingSettingsManager.Instance.SeverityConfigurations)
            {
                var inspection = (from nodes in xDocRoot.Elements()
                    let xAttribute = nodes.Attribute("Id")
                    where xAttribute != null && xAttribute.Value == ins.Id
                    select nodes).FirstOrDefault();
                if (inspection == null)
                {
                    var details = new XElement("Details",
                        new XAttribute("DefaultSeverity", ins.DefaultSeverity),
                        new XAttribute("Group", ins.GroupId),
                        new XAttribute("SolutionWide", ins.SolutionAnalysisRequired ? "yes" : "no"),
                        new XAttribute("Configurable", "yes"),
                        new XAttribute("CompoundName", ins.CompoundItemName ?? ""),
                        new XAttribute("QuickFixes", "")
                    );
                    AddFeature(xDocRoot, ins.Id, ins.GetType(), "Inspection", version,
                        ins.FullTitle, ins.Description, GetLangsForInspection(ins.Id), null, details);
                }

                insTypeNumber++;
            }

            xDoc.Add(xDocRoot);
            SynchronizeWithStaticDesciription(xDoc);
            xDoc.Save(fileName);
            MessageBox.ShowInfo("ReSharper features exported successfully.");
        }

        private string GetLangsForInspection(string id)
        {
            var lang = string.Empty;
            var langs = HighlightingSettingsManager.Instance.GetInspectionImplementations(id);
            foreach (var psiLanguageType in langs)
            {
                var langName = NormalizeLanguage(psiLanguageType.Name);
                if (!lang.Contains(langName))
                    lang += langName + ",";
            }

            lang = lang == string.Empty ? "all" : lang.TrimEnd(',');
            return lang;
        }

        private void SynchronizeWithStaticDesciription(XDocument xDoc)
        {
            var staticDescriptionXml = new XDocument();
            var file =
                Assembly.GetExecutingAssembly().GetPath().Directory.Directory
                    .Combine("lib/FeatureDescriptionStitic.xml");
            if (!file.ExistsFile)
            {
                staticDescriptionXml.Add(new XElement("Features"));
                foreach (var featureNode in xDoc.Root.Elements())
                    staticDescriptionXml.Root.Add(new XElement("Feature",
                        new XAttribute("Id", featureNode.Attribute("Id").Value),
                        new XAttribute("SinceVersion", featureNode.Attribute("SinceVersion").Value)));
            }
            else
            {
                try
                {
                    staticDescriptionXml = XDocument.Load(file.FullPath);
                }
                catch (XmlException exception)
                {
                    MessageBox.ShowInfo("FeatureDescriptionStitic.xml is corrupted... /n" + exception);
                }
            }

            foreach (var featureNode in xDoc.Root.Elements())
            {
                var staticFeatureNode = (from nodes in staticDescriptionXml.Root.Elements()
                    where nodes.Attribute("Id").Value == featureNode.Attribute("Id").Value
                    select nodes).FirstOrDefault();
                if (staticFeatureNode == null)
                {
                    staticDescriptionXml.Root.Add(new XElement("Feature",
                        new XAttribute("Id", featureNode.Attribute("Id").Value),
                        new XAttribute("SinceVersion", featureNode.Attribute("SinceVersion").Value)));
                }
                else
                {
                    foreach (var childElement in staticFeatureNode.Elements())
                    {
                        if (featureNode.Element(childElement.Name) != null)
                            featureNode.Element(childElement.Name).Remove();

                        featureNode.Add(childElement);
                    }

                    foreach (var attribute in staticFeatureNode.Attributes())
                    {
                        if (featureNode.Attribute(attribute.Name) != null)
                            featureNode.Attribute(attribute.Name).Remove();

                        featureNode.Add(attribute);
                    }
                }
            }

            foreach (var staticFeature in staticDescriptionXml.Root.Elements())
            {
                var dynamicFeatureNode = (from nodes in xDoc.Root.Elements()
                    where nodes.Attribute("Id").Value == staticFeature.Attribute("Id").Value
                    select nodes).FirstOrDefault();
                if (dynamicFeatureNode == null && staticFeature.Attribute("DeprecatedSince") == null)
                    staticFeature.Add(new XAttribute("DeprecatedSince", version));
            }

            staticDescriptionXml.Save(file.FullPath);
        }

        /// <summary>
        /// Builds a dictionary (feature type, test object)
        /// </summary>
        /// <returns></returns>
/*        private List<KeyValuePair<Type, Object>> GetTestsForFeatures()
        {
            var cache = new List<KeyValuePair<Type, Object>>();
            foreach (var assembly in this.myTestAssemblies)
            {
                foreach (var testType in assembly.GetTypes())
                {
                    if (testType.IsAbstract || testType.IsInterface || testType.Name.Contains("Availability")) continue;
                    var matchFound = false;
                    var ignore = false;

                    foreach (var attribute in testType.GetCustomAttributes(false))
                        if (attribute.GetType().Name == "IgnoreAttribute") ignore = true;

                    if (ignore) continue;



                    foreach (Type genericTypeArgument in testType.BaseType.GetGenericArguments())
                    {
                        if (genericTypeArgument != null && !testType.ContainsGenericParameters)
                        {
                            cache.Add(new KeyValuePair<Type, Object>(genericTypeArgument, Activator.CreateInstance(testType)));
                            matchFound = true;
                        }
                    }

                    var methods = testType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var methodInfo in methods)
                    {
                        if (methodInfo.Name == "CreateContextAction")
                        {
                            if (methodInfo.GetParameters().Length == 1)
                            {
                                var parametersArray = new object[] { null };
                                var testTypeInstance = Activator.CreateInstance(testType);
                                try
                                {
                                    object result = methodInfo.Invoke(testTypeInstance, parametersArray);
                                    cache.Add(new KeyValuePair<Type, Object>(result.GetType(), testTypeInstance));
                                }
                                catch (Exception e)
                                {
                                    MessageBox.ShowError(e.ToString());
                                }

                                matchFound = true;
                            }
                        }
                    }
                    if (!matchFound && testType.Name.EndsWith("Test"))
                        this.myUnmatchedTests.Add(testType);
                }
            }
            return cache;
        }*/

        /// <summary>
        ///     Finds all tests for a feature.
        /// </summary>
        /// <param name="examplesNode">Node for all examples</param>
        /// <param name="featureType">Feature type</param>
        /// <param name="cache">Cache </param>
        private void FindTestsByType(XElement examplesNode, Type featureType, List<KeyValuePair<Type, object>> cache)
        {
            var testFound = false;
            foreach (var cacheEntry in cache)
                if (cacheEntry.Key == featureType)
                {
                    var example = new XElement("Example");
                    var testTypeInstance = cacheEntry.Value;
                    ExtractExampleFromTest(example, testTypeInstance);
                    examplesNode.Add(example);
                    if (!example.IsEmpty) testFound = true;
                }

            // if we failed to find tests with reflection, we try to find something by name....
            if (!testFound)
                foreach (var unmatchedTest in myUnmatchedTests)
                {
                    var commonNamePart = string.Empty;

                    if (featureType.Name.Contains("Action"))
                        commonNamePart = featureType.Name.Substring(0, featureType.Name.LastIndexOf("Action"));

                    if (featureType.Name.Contains("ContextAction"))
                        commonNamePart = featureType.Name.Substring(0, featureType.Name.LastIndexOf("ContextAction"));

                    if (featureType.Name.Contains("Fix"))
                        commonNamePart = featureType.Name.Substring(0, featureType.Name.LastIndexOf("Fix"));

                    if (!string.IsNullOrEmpty(commonNamePart) && unmatchedTest.Name.StartsWith(commonNamePart))
                    {
                        var example = new XElement("Example");
                        var testTypeInstance = Activator.CreateInstance(unmatchedTest);
                        ExtractExampleFromTest(example, testTypeInstance);
                        examplesNode.Add(example);
                    }
                }
        }

        /// <summary>
        ///     Adds a single XML node for a feature
        /// </summary>
        private void AddFeature(XElement xDocRoot,
            string featureId,
            Type type,
            string featureType,
            string version,
            string name,
            string description,
            string lang,
            List<KeyValuePair<Type, object>> cache,
            XElement details
        )
        {
            if (string.IsNullOrEmpty(lang)) lang = GetLanguage(type);
            if (string.IsNullOrEmpty(name) || name == "Unknown") name = SplitCamelCase(type.Name);

            var featureNode = (from xml2 in xDocRoot.Descendants("Feature")
                let xElement = xml2.Attribute("Id")
                where xElement != null && xElement.Value == featureId
                select xml2).FirstOrDefault();

            XElement eamplesNode = null;
            XAttribute langAttr = null;

            if (featureNode == null)
            {
                langAttr = new XAttribute("Language", lang);
                eamplesNode = new XElement("Examples");
                if (featureType != "Inspection")
                    FindTestsByType(eamplesNode, type, cache);

                featureNode = new XElement("Feature",
                    new XAttribute("Type", featureType),
                    new XAttribute("Id", featureId),
                    langAttr,
                    new XAttribute("SinceVersion", version),
                    new XElement("Title", name),
                    new XElement("Description", description),
                    eamplesNode,
                    details
                );
                xDocRoot.Add(featureNode);
            }
            else
            {
                eamplesNode = featureNode.Element("Examples");
                langAttr = featureNode.Attribute("Language");

                if (!langAttr.Value.Contains(GetLanguage(type)) && GetLanguage(type) != "all")
                    langAttr.Value = langAttr.Value + "," + GetLanguage(type);
                FindTestsByType(eamplesNode, type, cache);

                var existingDesc = featureNode.Element("Description");
                if (description.Length > existingDesc.Value.Length)
                    existingDesc.Value = description;
            }

            if (eamplesNode != null)
                foreach (var example in eamplesNode.Elements())
                {
                    if (example.Attribute("Code") == null)
                        continue;
                    if (example.Attribute("Code").Value.Contains("+"))
                        continue;
                    if (langAttr.Value == "all")
                    {
                        langAttr.Value = example.Attribute("Code").Value;
                    }
                    else
                    {
                        if (!langAttr.Value.Contains(example.Attribute("Code").Value))
                            langAttr.Value = langAttr.Value + "," + example.Attribute("Code").Value;
                    }
                }
        }

        private string NormalizeLanguage(string input)
        {
            input = input.ToLower();
            switch (input)
            {
                case "vbasic": return "vb";
                case "java_script": return "javascript";
                case "web.config": return "webconfig";
                case "aspx": return "asp";
                case "asxx": return "asp";
                case "any": return "all";
                case ".cs": return "csharp";
                case ".vb": return "vb";
                case ".css": return "css";
                case ".html": return "html";
                case ".js": return "javascript";
                case ".xml": return "xml";
                case ".xaml": return "xaml";
                case ".aspx": return "asp";
                case ".ascx": return "asp";
                case ".master": return "asp";
                case ".proj": return "buildscript";
                case ".build": return "buildscript";
                case ".config": return "webconfig";
                case ".cshtml": return "razor";
            }

            return input;
        }

        private static string SplitCamelCase(string input)
        {
            //      if (input.Contains("QuickFix")) input = input.Replace("QuickFix", "");
            //      if (input.Contains("Fix")) input = input.Replace("Fix", "");
            var splitString = Regex.Replace(input, "([A-Z])", " $1",
                RegexOptions.Compiled).Trim();
            return splitString.Substring(0, 1) + splitString.Substring(1).ToLower();
        }

        /// <summary>
        ///     Tries to find one examples as code extract from test files
        /// </summary>
        /// <param name="example">Root XML element for the example</param>
        /// <param name="testTypeInstance">Instanciated test type</param>
        private void ExtractExampleFromTest(XElement example, object testTypeInstance)
        {
            var tetstPathProperty = testTypeInstance.GetType().GetProperty("TestDataPath2");
            var path = tetstPathProperty.GetValue(testTypeInstance, null).ToString();
            if (!Directory.Exists(path)) return;

            var testFiles = Directory.GetFiles(path);
            var resultFiles = Directory.GetFiles(path, "*.gold");
            var testWithAttribute = string.Empty;
            var targetTestFile = string.Empty;

            var testMethods = testTypeInstance.GetType().GetMethods(BindingFlags.DeclaredOnly |
                                                                    BindingFlags.Public | BindingFlags.Instance);

            foreach (var testMethod in testMethods)
            {
                var attributes = testMethod.GetCustomAttributes(false);
                foreach (var attribute in attributes)
                    if (testWithAttribute == string.Empty && attribute.GetType().Name == "TestAttribute")
                        testWithAttribute = testMethod.Name.ToLower();

                //TODO: This needs to be restored
                //          if (attribute.GetType() == typeof(FeatureExampleAttribute))
                //            testWithAttribute = testMethod.Name.ToLower();
            }

            foreach (var testFile in testFiles)
            {
                var testFileWithoutExtension = Path.GetFileNameWithoutExtension(testFile).ToLower();
                if (testWithAttribute.Length > 4 &&
                    (testFileWithoutExtension == testWithAttribute.Remove(0, 4) ||
                     testFileWithoutExtension == testWithAttribute))
                {
                    targetTestFile = testFile;
                    break;
                }

                foreach (var resultFile in resultFiles)
                    if (Path.GetFileName(testFile).ToLower() == Path.GetFileNameWithoutExtension(resultFile).ToLower())
                    {
                        targetTestFile = testFile;
                        break;
                    }
            }

            var targetAdditionalFile = string.Empty;
            if (targetTestFile == string.Empty) return;
            foreach (var helperTestFile in testFiles)
                if ((Path.GetFileNameWithoutExtension(targetTestFile) == Path.GetFileName(helperTestFile) ||
                     Path.GetFileNameWithoutExtension(helperTestFile) == Path.GetFileName(targetTestFile) ||
                     Path.GetFileNameWithoutExtension(helperTestFile) ==
                     Path.GetFileNameWithoutExtension(targetTestFile)) &&
                    Path.GetExtension(helperTestFile) != ".gold" &&
                    helperTestFile != targetTestFile)
                    targetAdditionalFile = helperTestFile;

            var addtitionalGoldFile = targetAdditionalFile + ".gold";
            var goldFile = targetTestFile + ".gold";
            if (!File.Exists(goldFile)) return;

            var before = File.ReadAllText(targetTestFile);
            var after = File.ReadAllText(goldFile);

            var extension = NormalizeLanguage(Path.GetExtension(targetTestFile));

            if (Path.GetExtension(targetTestFile) == ".cs" && Path.GetExtension(targetAdditionalFile) == ".vb" ||
                Path.GetExtension(targetTestFile) == ".vb" && Path.GetExtension(targetAdditionalFile) == ".cs")
                targetAdditionalFile = string.Empty;

            if (targetAdditionalFile != string.Empty)
            {
                var separator = Environment.NewLine + Environment.NewLine +
                                "//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++//" +
                                Environment.NewLine + Environment.NewLine;

                extension += " + " + NormalizeLanguage(Path.GetExtension(targetAdditionalFile));
                before += separator + File.ReadAllText(targetAdditionalFile);
                if (File.Exists(addtitionalGoldFile))
                    after += separator + File.ReadAllText(addtitionalGoldFile);
            }

            example.Add(new XAttribute("Code", extension));
            example.Add(new XElement("Before", before));
            example.Add(new XElement("After", after));

            // Debug
            example.Add(new XElement("TestPath", path));
            example.Add(new XElement("TestTypeName", testTypeInstance.GetType().FullName));
        }

        /// Gets language name for a feature type by the full type name. In lowercase
        private string GetLanguage(Type type)
        {
            var langNames = new[]
            {
                "csharp", "vb", "asp", "javascript", "buildscript", "html",
                "xml", "webconfig", "resx", "xaml", "css", "razor"
            };

            foreach (var language in langNames)
                if (type.FullName.ToLower().Contains(language))
                    return language;
            return "all";
        }
    }
}