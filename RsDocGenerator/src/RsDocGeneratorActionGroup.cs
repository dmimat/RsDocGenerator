﻿using JetBrains.Application.UI.Actions.MenuGroups;
using JetBrains.Application.UI.ActionSystem.ActionsRevised.Menu;

namespace RsDocGenerator
{
    [ActionGroup("RsDocGeneratorActionGroup", ActionGroupInsertStyles.Submenu, Text = "Generate content for documentation", Id = 4385)]
    class RsDocGeneratorActionGroup : IAction, IInsertLast<MainMenuFeaturesGroup>
    {
        public RsDocGeneratorActionGroup(RsDocExportMacros macros, RsDocExportContextActions ca,
            RsDocExportShortcuts shortcuts, RsDocExportTemplates templates, RsDocExportFixInScope scope,
            RsDocExportCodeInspections inspections, RsDocExportInspectionsIndex inspIndex,
            RsDocExportPostfixTemplates postfix, RsDocUpdateCatalog catalog, RsDocUpdateAll all)
        {
        }
    }
}
