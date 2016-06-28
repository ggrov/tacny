using System;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DafnyLanguage.TacnyLanguage
{
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [Guid(PackageGuidString)]
  [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
  public sealed class TacticReplacerCommandPackage : Package
  {
    public const string PackageGuidString = "f982f63a-aa1f-421d-9400-f3c10896146b";
    public TacticReplacerCommandFilter Trcf { get; set; }
    
    #region Package Members
    
    public const int CommandId = 0x0100;
    public static readonly Guid CommandSet = new Guid("6800196a-f13c-4bd6-a795-456a2eb74164");
    
    protected override void Initialize()
    {
      base.Initialize();

      var commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
      if (commandService == null) return;

      var menuCommandId = new CommandID(CommandSet, CommandId);
      var menuItem = new MenuCommand(MenuItemCallback, menuCommandId);
      commandService.AddCommand(menuItem);
    }
    private void MenuItemCallback(object sender, EventArgs e)
    {
      Trcf.Exec();
    }

    #endregion
  }
}
