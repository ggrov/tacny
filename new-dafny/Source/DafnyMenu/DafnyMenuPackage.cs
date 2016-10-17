using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using OLEServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace DafnyLanguage.DafnyMenu
{

  public interface IMenuProxy
  {
    int ToggleSnapshotVerification(IWpfTextView activeTextView);

    int ToggleMoreAdvancedSnapshotVerification(IWpfTextView activeTextView);

    bool MoreAdvancedSnapshotVerificationCommandEnabled(IWpfTextView activeTextView);

    bool ToggleAutomaticInduction(IWpfTextView activeTextView);

    bool AutomaticInductionCommandEnabled(IWpfTextView activeTextView);

    bool StopVerifierCommandEnabled(IWpfTextView activeTextView);

    void StopVerifier(IWpfTextView activeTextView);

    bool RunVerifierCommandEnabled(IWpfTextView activeTextView);

    void RunVerifier(IWpfTextView activeTextView);

    bool StopResolverCommandEnabled(IWpfTextView activeTextView);

    void StopResolver(IWpfTextView activeTextView);

    bool RunResolverCommandEnabled(IWpfTextView activeTextView);

    void RunResolver(IWpfTextView activeTextView);

    bool MenuEnabled(IWpfTextView activeTextView);

    bool CompileCommandEnabled(IWpfTextView activeTextView);

    void Compile(IWpfTextView activeTextView);

    bool ShowErrorModelCommandEnabled(IWpfTextView activeTextView);

    void ShowErrorModel(IWpfTextView activeTextView);

    bool DiagnoseTimeoutsCommandEnabled(IWpfTextView activeTextView);

    void DiagnoseTimeouts(IWpfTextView activeTextView);

    void GoToDefinition(IWpfTextView activeTextView);

    bool ToggleTacticEvaluation();
  }

  public interface ITacnyMenuProxy
  {
    bool ReplaceOneCall(IWpfTextView atv);
    bool ShowRot(IWpfTextView atv);
    bool ReplaceAll(ITextBuffer tb);
    void AddUpdaterForRot(IPeekSession session, Action<string> recalculate);
    bool UpdateRot(string file, ITextSnapshot snapshot);
    bool ClearPeekSession(IPeekSession session);
    Tuple<string, string> GetExpandedForPeekSession(IPeekSession session);
    bool CanExpandAtThisPosition(IWpfTextView tv);
  }

  public interface IDeadCodeMenuProxy
  {
    bool Toggle();
    ISuggestedAction GetSuggestedAction(ITextView tv, int dali);
  }

  /// <summary>
  /// This is the class that implements the package exposed by this assembly.
  ///
  /// The minimum requirement for a class to be considered a valid package for Visual Studio
  /// is to implement the IVsPackage interface and register itself with the shell.
  /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
  /// to do it: it derives from the Package class that provides the implementation of the
  /// IVsPackage interface and uses the registration attributes defined in the framework to
  /// register itself and its components with the shell.
  /// </summary>
  // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
  // a package.
  [PackageRegistration(UseManagedResourcesOnly = true)]
  // This attribute is used to register the information needed to show this package
  // in the Help/About dialog of Visual Studio.
  // [InstalledProductRegistration("#110", "#112", "1.0")]
  // This attribute is needed to let the shell know that this package exposes some menus.
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [ProvideAutoLoad("{6c7ed99a-206a-4937-9e08-b389de175f68}")]
  [ProvideToolWindow(typeof(BvdToolWindow), Transient = true)]
  [ProvideToolWindowVisibility(typeof(BvdToolWindow), GuidList.guidDafnyMenuCmdSetString)]
  [Guid(GuidList.guidDafnyMenuPkgString)]
  public sealed class DafnyMenuPackage : Package
  {
    #region fields
    private OleMenuCommand compileCommand;
    private OleMenuCommand menuCommand;
    private OleMenuCommand runVerifierCommand;
    private OleMenuCommand stopVerifierCommand;
    private OleMenuCommand runResolverCommand;
    private OleMenuCommand stopResolverCommand;
    private OleMenuCommand toggleSnapshotVerificationCommand;
    private OleMenuCommand toggleMoreAdvancedSnapshotVerificationCommand;
    private OleMenuCommand toggleAutomaticInductionCommand;
    private OleMenuCommand toggleBVDCommand;
    private OleMenuCommand diagnoseTimeoutsCommand;

    private OleMenuCommand expandAllCommand;
    private OleMenuCommand toggleCommand;
    private OleMenuCommand removeAllDeadCodeCommand;
    private OleMenuCommand toggleDeadCodeCommand;

    private OleMenuCommand contextMenuCommand;
    private OleMenuCommand contextExpandTacticsCommand;
    private OleMenuCommand contextExpandRotCommand;
    private OleMenuCommand contextRemoveDeadCodeCommand;
    private OleMenuCommand contextRemoveDeadMemberCodeCommand;

    bool BVDDisabled;

    public IMenuProxy MenuProxy { get; set; }
    public ITacnyMenuProxy TacnyMenuProxy { get; set; }
    public IDeadCodeMenuProxy DeadCodeMenuProxy { get; set; }
    #endregion

    /// <summary>
    /// Default constructor of the package.
    /// Inside this method you can place any initialization code that does not require
    /// any Visual Studio service because at this point the package object is created but
    /// not sited yet inside Visual Studio environment. The place to do all the other
    /// initialization is the Initialize method.
    /// </summary>
    public DafnyMenuPackage()
    {
      Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
    }


    #region Package Members

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    protected override void Initialize()
    {
      Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
      base.Initialize();

      // Add our command handlers for menu (commands must exist in the .vsct file)
      var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
      if (null != mcs)
      {
        #region main dafny menu items
        var compileCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidCompile);
        compileCommand = new OleMenuCommand(CompileCallback, compileCommandID);
        compileCommand.Enabled = false;
        compileCommand.BeforeQueryStatus += compileCommand_BeforeQueryStatus;
        mcs.AddCommand(compileCommand);

        var runVerifierCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidRunVerifier);
        runVerifierCommand = new OleMenuCommand(RunVerifierCallback, runVerifierCommandID);
        runVerifierCommand.Enabled = true;
        runVerifierCommand.BeforeQueryStatus += runVerifierCommand_BeforeQueryStatus;
        mcs.AddCommand(runVerifierCommand);

        var stopVerifierCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidStopVerifier);
        stopVerifierCommand = new OleMenuCommand(StopVerifierCallback, stopVerifierCommandID);
        stopVerifierCommand.Enabled = true;
        stopVerifierCommand.BeforeQueryStatus += stopVerifierCommand_BeforeQueryStatus;
        mcs.AddCommand(stopVerifierCommand);

        var runResolverCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidRunResolver);
        runResolverCommand = new OleMenuCommand(RunResolverCallback, runResolverCommandID);
        runResolverCommand.Enabled = true;
        runResolverCommand.BeforeQueryStatus += runResolverCommand_BeforeQueryStatus;
        mcs.AddCommand(runResolverCommand);

        var stopResolverCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidStopResolver);
        stopResolverCommand = new OleMenuCommand(StopResolverCallback, stopResolverCommandID);
        stopResolverCommand.Enabled = true;
        stopResolverCommand.BeforeQueryStatus += stopResolverCommand_BeforeQueryStatus;
        mcs.AddCommand(stopResolverCommand);

        var toggleSnapshotVerificationCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleSnapshotVerification);
        toggleSnapshotVerificationCommand = new OleMenuCommand(ToggleSnapshotVerificationCallback, toggleSnapshotVerificationCommandID);
        mcs.AddCommand(toggleSnapshotVerificationCommand);

        var toggleMoreAdvancedSnapshotVerificationCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleMoreAdvancedSnapshotVerification);
        toggleMoreAdvancedSnapshotVerificationCommand = new OleMenuCommand(ToggleMoreAdvancedSnapshotVerificationCallback, toggleMoreAdvancedSnapshotVerificationCommandID);
        toggleMoreAdvancedSnapshotVerificationCommand.Enabled = true;
        toggleMoreAdvancedSnapshotVerificationCommand.BeforeQueryStatus += toggleMoreAdvancedSnapshotVerificationCommand_BeforeQueryStatus;
        mcs.AddCommand(toggleMoreAdvancedSnapshotVerificationCommand);

        var toggleAutomaticInductionCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleAutomaticInduction);
        toggleAutomaticInductionCommand = new OleMenuCommand(ToggleAutomaticInductionCallback, toggleAutomaticInductionCommandID);
        toggleAutomaticInductionCommand.Enabled = true;
        toggleAutomaticInductionCommand.BeforeQueryStatus += toggleAutomaticInductionCommand_BeforeQueryStatus;
        mcs.AddCommand(toggleAutomaticInductionCommand);

        var showErrorModelCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleBVD);
        toggleBVDCommand = new OleMenuCommand(ToggleBVDCallback, showErrorModelCommandID);
        toggleBVDCommand.Enabled = true;
        toggleBVDCommand.BeforeQueryStatus += showErrorModelCommand_BeforeQueryStatus;
        mcs.AddCommand(toggleBVDCommand);

        var diagnoseTimeoutsCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidDiagnoseTimeouts);
        diagnoseTimeoutsCommand = new OleMenuCommand(DiagnoseTimeoutsCallback, diagnoseTimeoutsCommandID);
        diagnoseTimeoutsCommand.Enabled = true;
        diagnoseTimeoutsCommand.BeforeQueryStatus += diagnoseTimeoutsCommand_BeforeQueryStatus;
        mcs.AddCommand(diagnoseTimeoutsCommand);

        var menuCommandID = new CommandID(GuidList.guidDafnyMenuPkgSet, (int)PkgCmdIDList.DafnyMenu);
        menuCommand = new OleMenuCommand(new EventHandler((sender, e) => { }), menuCommandID);
        menuCommand.BeforeQueryStatus += menuCommand_BeforeQueryStatus;
        menuCommand.Enabled = true;
        mcs.AddCommand(menuCommand);

        var toggleDeadCodeCommandId = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleDeadCode);
        toggleDeadCodeCommand = new OleMenuCommand(ToggleDeadCode, toggleDeadCodeCommandId);
        mcs.AddCommand(toggleDeadCodeCommand);
        #endregion

        #region refactoring menu
        var expandAllCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidExpandAllTactics);
        expandAllCommand = new OleMenuCommand(TacticReplaceAllCallback, expandAllCommandId);
        expandAllCommand.Visible = expandAllCommand.Enabled = true;
        mcs.AddCommand(expandAllCommand);

        var toggleCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidToggleTacny);
        toggleCommand = new OleMenuCommand(ToggleItemCallback, toggleCommandId);
        mcs.AddCommand(toggleCommand);

        var removeAllDeadCodeCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidRemoveAllDeadCode);
        removeAllDeadCodeCommand = new OleMenuCommand(RemoveDeadCode, removeAllDeadCodeCommandId);
        removeAllDeadCodeCommand.BeforeQueryStatus += RemoveAllDeadCodeBeforeQuery;
        mcs.AddCommand(removeAllDeadCodeCommand);
        #endregion

        #region refactoring context menu
        var contextExpandTacticsCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidContextExpandTactics);
        contextExpandTacticsCommand = new OleMenuCommand(TacticReplaceCallback, contextExpandTacticsCommandId);
        contextExpandTacticsCommand.BeforeQueryStatus += RefactorTacticsContextBeforeQuery;
        mcs.AddCommand(contextExpandTacticsCommand);

        var contextExpandRotCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidContextExpandRot);
        contextExpandRotCommand = new OleMenuCommand(ShowRotCallback, contextExpandRotCommandId);
        contextExpandRotCommand.BeforeQueryStatus += RefactorTacticsContextBeforeQuery;
        mcs.AddCommand(contextExpandRotCommand);

        var contextRemoveDeadCodeCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidContextRemoveDeadCode);
        contextRemoveDeadCodeCommand = new OleMenuCommand(RemoveDeadCode, contextRemoveDeadCodeCommandId);
        contextRemoveDeadCodeCommand.BeforeQueryStatus += RemoveDeadCodeBeforeQuery;
        mcs.AddCommand(contextRemoveDeadCodeCommand);

        var contextRemoveDeadMemberCodeCommandId = new CommandID(GuidList.guidRefactoringMenuCmdSet, (int)PkgCmdIDList.cmdidContextRemoveDeadMemberCode);
        contextRemoveDeadMemberCodeCommand = new OleMenuCommand(RemoveDeadCode, contextRemoveDeadMemberCodeCommandId);
        contextRemoveDeadMemberCodeCommand.BeforeQueryStatus += RemoveDeadMemberCodeBeforeQuery;
        mcs.AddCommand(contextRemoveDeadMemberCodeCommand);

        var ctxtCommandId = new CommandID(GuidList.guidDafnyMenuPkgSet, (int)PkgCmdIDList.RefactoringContextMenu);
        contextMenuCommand = new OleMenuCommand(new EventHandler((sender, e) => { }), ctxtCommandId);
        contextMenuCommand.BeforeQueryStatus += ContextCommandBeforeQuery;
        mcs.AddCommand(contextMenuCommand);
        #endregion
      }
    }

    internal IVsEditorAdaptersFactoryService editorAdapterFactoryService = null;

    private IWpfTextView ActiveTextView
    {
      get
      {
        var textManager = (IVsTextManager)GetService(typeof(SVsTextManager));
        if (textManager != null)
        {
          IVsTextView view;
          if (textManager.GetActiveView(1, null, out view) == Microsoft.VisualStudio.VSConstants.S_OK)
          {
            if (editorAdapterFactoryService == null)
            {
              var componentModel = (IComponentModel)GetService(typeof(SComponentModel));
              if (componentModel != null)
              {
                editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();
              }
            }
            if (editorAdapterFactoryService != null)
            {
              var textView = editorAdapterFactoryService.GetWpfTextView(view);
              if (textView != null)
              {
                return textView;
              }
            }
          }
        }
        return null;
      }
    }
    
    private void ContextCommandBeforeQuery(object s, EventArgs e) {
      var atv = ActiveTextView;
      var enabled = TacnyMenuProxy != null && DeadCodeMenuProxy != null &&
        atv?.TextBuffer != null && atv.TextBuffer.ContentType.IsOfType("dafny");
      var cmd = s as OleMenuCommand;
      if (cmd == null) return;
      cmd.Visible = cmd.Enabled = enabled;
    }

    #region dafny menu methods
    void ToggleSnapshotVerificationCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var mode = MenuProxy.ToggleSnapshotVerification(atv);
        toggleSnapshotVerificationCommand.Text = (mode == 1 ? "Disable" : "Enable") + " on-demand re-verification";
        toggleMoreAdvancedSnapshotVerificationCommand.Text = (mode == 2 ? "Disable" : "Enable") + " more advanced on-demand re-verification";
      }
    }

    void ToggleMoreAdvancedSnapshotVerificationCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var mode = MenuProxy.ToggleMoreAdvancedSnapshotVerification(atv);
        toggleSnapshotVerificationCommand.Text = (mode != 0 ? "Disable" : "Enable") + " on-demand re-verification";
        toggleMoreAdvancedSnapshotVerificationCommand.Text = (mode == 2 ? "Disable" : "Enable") + " more advanced on-demand re-verification";
      }
    }

    void ToggleAutomaticInductionCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        var nowAutomatic = MenuProxy.ToggleAutomaticInduction(atv);
        toggleAutomaticInductionCommand.Text = (nowAutomatic ? "Disable" : "Enable") + " automatic induction";
      }
    }

    void stopVerifierCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var enabled = MenuProxy.StopVerifierCommandEnabled(atv);
        stopVerifierCommand.Visible = enabled;
        stopVerifierCommand.Enabled = enabled;
      }
    }

    void StopVerifierCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        MenuProxy.StopVerifier(atv);
      }
    }

    void runVerifierCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var enabled = MenuProxy.RunVerifierCommandEnabled(atv);
        runVerifierCommand.Visible = enabled;
        runVerifierCommand.Enabled = enabled;
      }
    }

    void RunVerifierCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        MenuProxy.RunVerifier(atv);
      }
    }

    void stopResolverCommand_BeforeQueryStatus(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        var enabled = MenuProxy.StopResolverCommandEnabled(atv);
        stopResolverCommand.Visible = enabled;
        stopResolverCommand.Enabled = enabled;
      }
    }

    void StopResolverCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        MenuProxy.StopResolver(atv);
      }
    }
    void runResolverCommand_BeforeQueryStatus(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        var enabled = MenuProxy.RunResolverCommandEnabled(atv);
        runResolverCommand.Visible = enabled;
        runResolverCommand.Enabled = enabled;
      }
    }

    void RunResolverCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        MenuProxy.RunResolver(atv);
      }
    }

    void menuCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var isActive = MenuProxy.MenuEnabled(atv);
        menuCommand.Visible = isActive;
        menuCommand.Enabled = isActive;
      }
    }

    void compileCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var enabled = MenuProxy.CompileCommandEnabled(atv);
        compileCommand.Enabled = enabled;
      }
    }

    void CompileCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        MenuProxy.Compile(atv);
      }
    }

    void showErrorModelCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var visible = MenuProxy.ShowErrorModelCommandEnabled(atv);
        toggleBVDCommand.Visible = visible;
      }
    }

    void diagnoseTimeoutsCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var visible = MenuProxy.DiagnoseTimeoutsCommandEnabled(atv);
        diagnoseTimeoutsCommand.Visible = visible;
      }
    }

    private void toggleMoreAdvancedSnapshotVerificationCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        var visible = MenuProxy.MoreAdvancedSnapshotVerificationCommandEnabled(atv);
        toggleMoreAdvancedSnapshotVerificationCommand.Visible = visible;
      }
    }

    private void toggleAutomaticInductionCommand_BeforeQueryStatus(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null) {
        var visible = MenuProxy.AutomaticInductionCommandEnabled(atv);
        toggleAutomaticInductionCommand.Visible = visible;
      }
    }

    void GotoDefinitionCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        MenuProxy.GoToDefinition(atv);
      }
    }

    void ToggleBVDCallback(object sender, EventArgs e)
    {
      BVDDisabled = !BVDDisabled;
      toggleBVDCommand.Text = (BVDDisabled ? "Enable" : "Disable") + " BVD";
    }

    void DiagnoseTimeoutsCallback(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      if (MenuProxy != null && atv != null)
      {
        MenuProxy.DiagnoseTimeouts(atv);
      }
    }

    public void ExecuteAsCompiling(Action action, TextWriter outputWriter)
    {
      IVsStatusbar statusBar = (IVsStatusbar)GetGlobalService(typeof(SVsStatusbar));
      uint cookie = 0;
      statusBar.Progress(ref cookie, 1, "Compiling...", 0, 0);

      var gowp = (IVsOutputWindowPane)GetService(typeof(SVsGeneralOutputWindowPane));
      if (gowp != null)
      {
        gowp.Clear();
      }

      action();

      if (gowp != null)
      {
        gowp.OutputStringThreadSafe(outputWriter.ToString());
        gowp.Activate();
      }

      statusBar.Progress(ref cookie, 0, "", 0, 0);
    }

    public void ShowErrorModelInBVD(string model, int id)
    {
      if (!BVDDisabled)
      {
        var window = this.FindToolWindow(typeof(BvdToolWindow), 0, true);
        if ((window == null) || (window.Frame == null))
        {
          throw new NotSupportedException("Cannot create BvdToolWindow.");
        }

        BvdToolWindow.BVD.HideMenuStrip();
        BvdToolWindow.BVD.HideStateList();
        BvdToolWindow.BVD.ReadModel(model);
        BvdToolWindow.BVD.SetState(id, true);
        BvdToolWindow.BVD.SetFont(new Font(SystemFonts.DefaultFont.FontFamily, 1.3f * SystemFonts.DefaultFont.Size, SystemFonts.DefaultFont.Style));

        IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
        Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
      }
    }

    public string TryToLookupValueInCurrentModel(string name, out bool wasUpdated)
    {
      string result = null;
      wasUpdated = false;
      if (!BVDDisabled && BvdToolWindow.BVD.LangModel != null)
      {
        var m = BvdToolWindow.BVD.LangModel as Microsoft.Boogie.ModelViewer.Dafny.DafnyModel;
        var s = m.states[BvdToolWindow.BVD.CurrentState];
        var v = s.Vars.FirstOrDefault(var => var.Name == name);
        if (v != null)
        {
          wasUpdated = v.updatedHere;
          result = m.CanonicalName(v.Element);
        }
      }
      return result;
    }
	
    public int GoToDefinition(string filePath, int lineNumber, int offset) {
      return (OpenFile(filePath) && GoToSource(filePath, lineNumber, offset)) ? VSConstants.S_OK : VSConstants.E_FAIL;
    }

    private bool OpenFile(string filePath) {
      var shellOpenDocumentService = (IVsUIShellOpenDocument)GetService(typeof(SVsUIShellOpenDocument));
      var guidTextView = VSConstants.LOGVIEWID.TextView_guid;

      uint itemid;
      IVsUIHierarchy hierarchy;
      OLEServiceProvider serviceProvider;
      IVsWindowFrame window;
      if (shellOpenDocumentService.OpenDocumentViaProject(filePath, ref guidTextView, out serviceProvider, out hierarchy, out itemid, out window) == VSConstants.S_OK) {
        window.Show();
        return true;
      }
      return false;
    }

    private bool GoToSource(string filePath, int lineNumber, int offset) {
      IVsRunningDocumentTable runningDoc = (IVsRunningDocumentTable)GetService(typeof(SVsRunningDocumentTable));
      IntPtr ptr;
      IVsHierarchy hierarchy;
      uint cookie;
      uint id;

      if (runningDoc.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, filePath, out hierarchy, out id, out ptr, out cookie) != VSConstants.S_OK) {
        return false;
      }
      try {
        var lines = Marshal.GetObjectForIUnknown(ptr) as IVsTextLines;
        if (lines == null) {
          return false;
        }
        var textManager = (IVsTextManager)GetService(typeof(SVsTextManager));
        if (textManager == null) {
          return false;
        }
        return textManager.NavigateToLineAndColumn(
            lines, VSConstants.LOGVIEWID.TextView_guid, lineNumber, offset, lineNumber, offset) == VSConstants.S_OK;
      } finally {
        if (ptr != IntPtr.Zero) {
          Marshal.Release(ptr);
        }
      }
    }
    #endregion

    #region tactics
    private void TacticReplaceCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (TacnyMenuProxy != null && atv != null) {
        TacnyMenuProxy.ReplaceOneCall(atv);
      }
    }

    private void ShowRotCallback(object sender, EventArgs e) {
      var atv = ActiveTextView;
      if (TacnyMenuProxy != null && atv != null) {
        TacnyMenuProxy.ShowRot(atv);
      }
    }

    private void TacticReplaceAllCallback(object s, EventArgs e) {
      var atv = ActiveTextView;
      if (TacnyMenuProxy != null && atv?.TextBuffer != null) {
        TacnyMenuProxy.ReplaceAll(atv.TextBuffer);
      }
    }

    private void ToggleItemCallback(object sender, EventArgs e) {
      if (MenuProxy == null) return;
      var result = MenuProxy.ToggleTacticEvaluation() ? "Disable" : "Enable";
      toggleCommand.Text = result + " Automatic Tactic Verification";
    }

    private void RefactorTacticsContextBeforeQuery(object sender, EventArgs e)
    {
      var atv = ActiveTextView;
      var menuitem = sender as OleMenuCommand;
      if (TacnyMenuProxy == null || atv?.TextBuffer == null || menuitem==null) return;
      menuitem.Enabled = TacnyMenuProxy.CanExpandAtThisPosition(atv);
    }
    #endregion

    #region dead code analysis
    private void ToggleDeadCode(object s, EventArgs e)
    {
      if (DeadCodeMenuProxy == null) return;
      var result = DeadCodeMenuProxy.Toggle() ? "Disable" : "Enable";
      toggleDeadCodeCommand.Text = result + " dead anotation analysis";
    }
    
    private void RemoveDeadCodeBeforeQuery(object s, EventArgs e) {
      var cmd = s as OleMenuCommand;
      if (cmd == null) return;
      UpdateDeadCodeButton(cmd, 0);
    }

    private void RemoveDeadMemberCodeBeforeQuery(object s, EventArgs e) {
      var cmd = s as OleMenuCommand;
      if (cmd == null) return;
      UpdateDeadCodeButton(cmd, 1);
    }

    private void RemoveAllDeadCodeBeforeQuery(object s, EventArgs e) {
      var cmd = s as OleMenuCommand;
      if (cmd == null) return;
      UpdateDeadCodeButton(cmd, 2);
    }

    private void UpdateDeadCodeButton(OleMenuCommand cmd, int action) {
      var atv = ActiveTextView;
      var enabled = DeadCodeMenuProxy != null && atv?.TextBuffer != null && atv.TextBuffer.ContentType.IsOfType("dafny");
      var act = DeadCodeMenuProxy?.GetSuggestedAction(atv, action);
      if (act == null) enabled = false;
      cmd.Properties.Remove("DeadCodeRemoval");
      cmd.Visible = cmd.Enabled = enabled;
      if (!enabled) return;
      cmd.Text = act.DisplayText;
      cmd.Properties.Add("DeadCodeRemoval", act);
    }

    private void RemoveDeadCode(object s, EventArgs e) {
      var atv = ActiveTextView;
      if (atv == null) return;
      var cmd = s as OleMenuCommand;
      if (cmd!=null && !cmd.Properties.Contains("DeadCodeRemoval")) return;
      var act = cmd?.Properties["DeadCodeRemoval"] as ISuggestedAction;
      act?.Invoke(CancellationToken.None);
    }
    #endregion

    #endregion
  }
}
