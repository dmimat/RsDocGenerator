using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Feature.Services.Resources;
using JetBrains.UI.Options;
using JetBrains.UI.Options.OptionPages.ToolsPages;
using JetBrains.UI.Options.OptionsDialog2.SimpleOptions;
using JetBrains.Util;

namespace RsDocGenerator
{
    [OptionsPage(PID, "Documentation Generator", typeof(FeaturesEnvironmentOptionsThemedIcons.GeneratedCode),
        ParentId = ToolsPage.PID)]
    class RsDocOptionsPage : SimpleOptionsPage
    {
        private const string PID = "RsDocGenerator";

        public RsDocOptionsPage(Lifetime lifetime, OptionsSettingsSmartContext optionsSettingsSmartContext)
            : base(lifetime, optionsSettingsSmartContext)
        {
            // output path
            IProperty<FileSystemPath> outputPath =
                new Property<FileSystemPath>(lifetime, "RsDocOptionsPage::OutputPath");
            outputPath.SetValue(FileSystemPath.TryParse(
                optionsSettingsSmartContext.StoreOptionsTransactionContext.GetValue(
                    (RsDocSettingsKey key) => key.RsDocOutputFolder)));
            outputPath.Change.Advise(lifetime, a =>
            {
                if (!a.HasNew || a.New == null) return;
                optionsSettingsSmartContext.StoreOptionsTransactionContext.SetValue(
                    (RsDocSettingsKey key) => key.RsDocOutputFolder, a.New.FullPath);
            });
            AddText("Output folder for generated content:");
            var outputPathOption = AddFolderChooserOption(outputPath, null, null);
            outputPathOption.IsEnabledProperty.SetValue(true);

            // ReSharper feature catalog path
            IProperty<FileSystemPath> featureCatalogPath =
                new Property<FileSystemPath>(lifetime, "RsDocOptionsPage::OutputPath");
            featureCatalogPath.SetValue(FileSystemPath.TryParse(
                optionsSettingsSmartContext.StoreOptionsTransactionContext.GetValue(
                    (RsDocSettingsKey key) => key.RsDocFeatureCatalog)));
            featureCatalogPath.Change.Advise(lifetime, a =>
            {
                if (!a.HasNew || a.New == null) return;
                optionsSettingsSmartContext.StoreOptionsTransactionContext.SetValue(
                    (RsDocSettingsKey key) => key.RsDocFeatureCatalog, a.New.FullPath);
            });
            AddText("Folder with ReSharper feature catalog (RsFeatureCatalog.xml):");
            var featureCataologOption = AddFolderChooserOption(featureCatalogPath, null, null);
            featureCataologOption.IsEnabledProperty.SetValue(true);

            // folder with samples for context actions
            IProperty<FileSystemPath> caFolder = new Property<FileSystemPath>(lifetime, "RsDocOptionsPage::CaFolder");
            caFolder.SetValue(FileSystemPath.TryParse(
                optionsSettingsSmartContext.StoreOptionsTransactionContext.GetValue(
                    (RsDocSettingsKey key) => key.RsDocCaFolder)));
            caFolder.Change.Advise(lifetime, a =>
            {
                if (!a.HasNew || a.New == null) return;
                optionsSettingsSmartContext.StoreOptionsTransactionContext.SetValue(
                    (RsDocSettingsKey key) => key.RsDocCaFolder, a.New.FullPath);
            });
            AddText("Folder with context actions samples:");
            var caFoolderOption = AddFolderChooserOption(caFolder, null, null);
            caFoolderOption.IsEnabledProperty.SetValue(true);
        }
    }

    [SettingsKey(typeof(EnvironmentSettings), "Documentation generator settings")]
    public class RsDocSettingsKey
    {
        [SettingsEntry("", "Output folder")] public string RsDocOutputFolder;
        [SettingsEntry("", "Folder with context actions samples")] public string RsDocCaFolder;
        [SettingsEntry("", "ReSharper feature catalog")] public string RsDocFeatureCatalog;
    }
}