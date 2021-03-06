﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JetBrains;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.UI.ActionsRevised;
using JetBrains.Util;

namespace RsDocGenerator
{
  [Action("RsDocExportContextActions", "Export Context Actions", Id = 8673421)]
  internal class RsDocExportContextActions : RsDocExportBase
  {
    protected override string GenerateContent(IDataContext context, string outputFolder)
    {
      const string caTopicId = "CA_Chunks";
      var caLibrary = XmlHelpers.CreateHmTopic(caTopicId);
      var tablesByLanguage = new Dictionary<string, XElement>();
      var sortedActions = context.GetComponent<IContextActionTable>().AllActions.OrderBy(ca => ca.Name);
      var caPath = GeneralHelpers.GetCaPath(context);

      caLibrary.Root.Add(
        new XComment("Total context actions in ReSharper " +
                     GeneralHelpers.GetCurrentVersion() + ": " +
                     sortedActions.Count()));
      foreach (var ca in sortedActions)
      {
        var lang = ca.Group ?? "Unknown";

        if (!tablesByLanguage.ContainsKey(lang))
          tablesByLanguage.Add(lang,
            XmlHelpers.CreateTwoColumnTable("Name",
              "Description", "40%"));

        var exampleTable = ExtractExamples(ca, caPath, lang);

        tablesByLanguage.GetValue(lang).Add(new XElement("tr",
          new XElement("td", new XElement("b", ca.Name)),
          new XElement("td",
            ca.Description ?? "", exampleTable,
            XmlHelpers.CreateInclude("CA_Static_Chunks",
              ca.MergeKey.NormalizeStringForAttribute()))));
      }

      foreach (var table in tablesByLanguage)
      {
        var languageChunk =
          XmlHelpers.CreateChunk("ca_" +
                                 table.Key
                                   .NormalizeStringForAttribute());
        string langText = table.Key == "Common"
          ? "common use"
          : table.Key;
        languageChunk.Add(new XElement("p",
          "ReSharper provides the following context actions for " +
          langText + ":"));
        languageChunk.Add(table.Value);
        caLibrary.Root.Add(languageChunk);
      }

      caLibrary.Save(Path.Combine(outputFolder, caTopicId + ".xml"));
      return "Context actions";
    }

    [CanBeNull]
    private XElement ExtractExamples(IContextActionInfo contextAction, string caPath, string lang)
    {
      // temporarily disabled
      return null;
      var testFileName = contextAction.MergeKey.Split('.').Last().RemoveFromEnd("Action") + ".cs";
      var goldFileName = testFileName + ".gold";
      var basePath = Path.Combine(caPath, lang.NormalizeStringForAttribute().ToLower());
      var testFile = Path.Combine(basePath, testFileName);
      if (!File.Exists(testFile)) return null;
      var goldFile = Path.Combine(basePath, goldFileName);
      if (!File.Exists(goldFile)) return null;

      var table = XmlHelpers.CreateTwoColumnTable("Before", "After", "50%");
      table.Add(new XElement("tr",
        new XElement("td", XmlHelpers.CreateCodeBlock(File.ReadAllText(testFile), lang)),
        new XElement("td", XmlHelpers.CreateCodeBlock(File.ReadAllText(goldFile), lang))));

      return table;
    }
  }
}