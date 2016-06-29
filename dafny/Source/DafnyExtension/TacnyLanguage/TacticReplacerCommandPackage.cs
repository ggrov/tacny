using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DafnyLanguage.TacnyLanguage
{
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [Guid(TacnyPackageIdentifiers.PackageGuidString)]
  public sealed class TacticReplacerCommandPackage : Package
  {
    public TacticReplacerCommandFilter Trcf { get; set; }
    
    protected override void Initialize()
    {
      base.Initialize();

      var commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
      if (commandService == null) return;

      var expandTacticsCommandId = new CommandID(TacnyPackageIdentifiers.CommandSetGuid, TacnyPackageIdentifiers.ExpandTacticsCommandId);
      var expandTacticsItem = new MenuCommand(MenuItemCallback, expandTacticsCommandId);
      commandService.AddCommand(expandTacticsItem);

      var expandAllCommandId = new CommandID(TacnyPackageIdentifiers.CommandSetGuid, TacnyPackageIdentifiers.ExpandAllTacticsCommandId);
      var expandAllItem = new MenuCommand(NotImplemented, expandAllCommandId);
      commandService.AddCommand(expandAllItem);

      var toggleCommandId = new CommandID(TacnyPackageIdentifiers.CommandSetGuid, TacnyPackageIdentifiers.DisableTacnyCommandId);
      var toggleItem = new MenuCommand(NotImplemented, toggleCommandId);
      commandService.AddCommand(toggleItem);
    }
    private void MenuItemCallback(object sender, EventArgs e)
    {
      Trcf.Exec();
    }

    private static void NotImplemented(object s, EventArgs e)
    {
      throw new NotImplementedException();
    }

  }
}
