using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using JetBrains.Annotations;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions;
using JetBrains.Application.UI.ActionsRevised.Menu;
using JetBrains.Application.UI.Extensions;
using JetBrains.Application.UI.Options;
using JetBrains.Application.UI.Options.OptionPages;
using JetBrains.Application.UI.Options.OptionsDialog;
using JetBrains.Application.UI.Options.OptionsDialog.SimpleOptions;
using JetBrains.Application.UI.Options.OptionsDialog.SimpleOptions.ViewModel;
using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.Util;

namespace RsDocGenerator
{
    [Action("Dump settings pages content")]
    public class RsDocExportOptions : IExecutableAction
    {
        private static string _optionsFile;
        private static XDocument _catalogDocument;
        private static XElement _currentParent;

        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            StartContentGeneration(context, GeneralHelpers.GetDotnetDocsRootFolder(context));
        }

        public static void StartContentGeneration(IDataContext context, string rootFolder)
        {
            if (rootFolder.IsNullOrEmpty()) return;
            _optionsFile = Path.Combine(rootFolder + "\\nonProject", "RsOptions.xml");
            _catalogDocument = new XDocument();
            _catalogDocument.Add(new XElement("Options"));
            _currentParent = _catalogDocument.Root;

//            Lifetime.Using(
//                lt =>
//                {
//                    var optionsDialogOwner = context.GetComponent<OptionsDialogOwner>();
//                    var dialog = optionsDialogOwner.Create(lt, null, null);
//                    var dialogOptionsAutomation = dialog.OptionsAutomation;
//
//                    var state = new OptionsPagesTraverseState();
//
//                    dialogOptionsAutomation.Pages.View(lt,
//                        (_, page) => { HandlePage(page, state, _currentParent); });
//
//                    TraverseChildren(
//                        dialog.Model.Options.OptionPagesTree.RootElement,
//                        state,
//                        descriptor => { dialog.OptionsAutomation.SelectPage(descriptor.Id); });
//                });
            _catalogDocument.Save(_optionsFile);
        }

//        private static void HandlePage(WrappedOptionPage page, OptionsPagesTraverseState state, XElement parent)
//        {
//            if (page?.Page == null)
//            {
//                WriteMessage(page.Id, state, $"Page is null. comment={page.Comment}", parent);
//                return;
//            }
//
//            var contentPage = page.Page;
//            var optionsPageAttribute = contentPage.GetType().GetAttribute<OptionsPageAttribute>();
//            if (optionsPageAttribute == null)
//            {
//                WriteMessage(page.Id, state, "Can't find OptionsPageAttribute", parent);
//                return;
//            }
//
//            TryDumpPage(contentPage, state, parent, optionsPageAttribute);
//        }

        private static void TryDumpPage(IOptionsPage page, OptionsPagesTraverseState state, XElement parent,
            OptionsPageAttribute optionsPageAttribute)
        {
            var compositeOptionPage = page as CompositeOptionPage;
            if (compositeOptionPage != null)
            {
                state.Indent++;
                foreach (var child in compositeOptionPage.Pages)
                    TryDumpPage(child, state, parent, optionsPageAttribute);
                state.Indent--;
            }
            else
            {
                TryDumpSimplePage(page, state, parent, optionsPageAttribute);
            }
        }

        private static void TryDumpSimplePage(IOptionsPage page, OptionsPagesTraverseState state, XElement parent,
            OptionsPageAttribute optionsPageAttribute)
        {
            var simpleOptionsPage = page as SimpleOptionsPage;
            if (simpleOptionsPage == null)
            {
                WriteMessage(page.Id, state, $"Unsupported options page {page.GetType()}", parent);
                return;
            }

            var dumper = new OptionsEntitiesDumper();
            var pageName = optionsPageAttribute.Name;
            WriteMessage(page.Id, state,
                $"Page. name=\"{pageName}\". id=\"{page.Id}\". help={optionsPageAttribute.HelpKeyword ?? "missedHelpKeyword"} Options count={simpleOptionsPage.OptionEntities.Count}",
                parent);
            state.Indent++;
            foreach (var optionEntity in simpleOptionsPage.OptionEntities)
            {
                var dump = dumper.DumpOption(optionEntity);
                foreach (var s in dump.Split('\r', '\n'))
                    if (!string.IsNullOrWhiteSpace(s))
                        parent.Add(new XElement("Option", s));
            }

            state.Indent--;
        }

        private static void WriteMessage(string id, OptionsPagesTraverseState state, string text, XElement parent)
        {
            var prefix = string.Empty.PadRight(state.Indent);
            parent.Add(new XElement("Page", $"{prefix}id={id}. {text}"));
        }

        private static void TraverseChildren(
            OptionsPageDescriptor descriptor,
            OptionsPagesTraverseState state,
            Action<OptionsPageDescriptor> execute)
        {
            execute(descriptor);
            state.Indent++;
            foreach (var child in descriptor.Children)
                TraverseChildren(child, state, execute);
            state.Indent--;
        }

        private class OptionsPagesTraverseState
        {
            public int Indent { get; set; }
        }

        private class OptionsEntitiesDumper
        {
            [NotNull] private readonly IDictionary<Type, Func<object, string>> myDumpersPerType;

            public OptionsEntitiesDumper()
            {
                myDumpersPerType =
                    new Dictionary<Type, Func<object, string>>
                    {
                        //CustomOption
                        [typeof(CustomOption)] = o => Dump((CustomOption) o),
                        [typeof(HeaderOptionViewModel)] = o => Dump((HeaderOptionViewModel) o),
                        [typeof(StringOptionViewModel)] = o => Dump((StringOptionViewModel) o),
                        [typeof(BoolOptionViewModel)] = o => Dump((BoolOptionViewModel) o),
                        [typeof(ButtonOptionViewModel)] = o => Dump((ButtonOptionViewModel) o),
                        [typeof(RichTextOptionViewModel)] = o => Dump((RichTextOptionViewModel) o),
                        [typeof(RadioOptionViewModel)] = o => Dump((RadioOptionViewModel) o),
                        [typeof(ComboOptionViewModel)] = o => Dump((ComboOptionViewModel) o),
                        [typeof(ComboEnumWithCaptionViewModelBase)] = o => Dump((ComboEnumWithCaptionViewModelBase) o),
                        [typeof(IntOptionViewModel)] = o => Dump((IntOptionViewModel) o),
                        [typeof(FolderChooserViewModel)] = o => Dump((FolderChooserViewModel) o),
                        [typeof(FileChooserViewModel)] = o => Dump((FileChooserViewModel) o)
                    };
            }

            public string DumpOption([NotNull] IOptionEntity optionEntity)
            {
                var res = string.Empty;
                var type = optionEntity.GetType();
                if (myDumpersPerType.TryGetValue(type, out var dump))
                    res = $"{dump(optionEntity)}";
                else
                    res = myDumpersPerType.TryGetValue(type.BaseType, out var baseDump)
                        ? $"{baseDump(optionEntity)}"
                        : "unknown entity type";

                return res;
            }

            private static string Dump(CustomOption vm)
            {
                return $"CustomOption. {vm.Automation.GetType().FullName}";
            }

            private static string Dump(HeaderOptionViewModel vm)
            {
                return vm.Text;
            }

            private static string Dump(StringOptionViewModel vm)
            {
//                return vm.Text;
                return null;
            }

            private static string Dump(BoolOptionViewModel vm)
            {
                return vm.RichText.ToString();
            }

            private static string Dump(ButtonOptionViewModel vm)
            {
                return vm.RichText.ToString();
            }

            private static string Dump(RichTextOptionViewModel vm)
            {
                return vm.RichText.ToString();
            }

            private static string Dump(RadioOptionViewModel vm)
            {
                var model = vm.RadioGroupViewModel.Model;
                var sb = new StringBuilder();
                sb.AppendLine(model.LabelText.Value.ToString());
                foreach (var child in model.RadioButtons)
                    sb.AppendLine($"-{child.Label.Value}");

                return sb.ToString();
            }

            private static string Dump(ComboOptionViewModel vm)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{vm.Prefix} + {vm.Suffix}");
                foreach (var point in vm.Points) sb.AppendLine($"-{point.Text}");

                return sb.ToString();
            }

            private static string Dump(ComboEnumWithCaptionViewModelBase vm)
            {
                return $"{vm.PrefixCaption};{vm.PostfixCaption}";
            }

            private static string Dump(IntOptionViewModel vm)
            {
                return $"{vm.PrefixCaption};{vm.PostfixCaption}";
            }

            private static string Dump(FolderChooserViewModel vm)
            {
                return $"{vm.ButtonText}";
            }

            private static string Dump(FileChooserViewModel vm)
            {
                return $"{vm.ButtonText}";
            }
        }
    }
}