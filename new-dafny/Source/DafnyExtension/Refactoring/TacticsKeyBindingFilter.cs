using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.Refactoring
{
  internal class TacticsKeyBindingFilter : IOleCommandTarget
  {
    private const uint ReplaceTacticF10 = (uint)VSConstants.VSStd97CmdID.StepOver;
    private const uint PeekTacticF9 = (uint)VSConstants.VSStd97CmdID.ToggleBreakpoint;

    private readonly IWpfTextView _tv;
    internal IOleCommandTarget Next;
    internal bool Added;
    
    public TacticsKeyBindingFilter(IWpfTextView tv)
    {
      _tv = tv;
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
      if (pguidCmdGroup != typeof(VSConstants.VSStd97CmdID).GUID || cCmds != 1)
        return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

      switch (prgCmds[0].cmdID)
      {
        case ReplaceTacticF10:
        case PeekTacticF9:
          prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED;
          prgCmds[0].cmdf |= (uint)OLECMDF.OLECMDF_ENABLED;
          return VSConstants.S_OK;
        default:
          return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
      }
    }

    int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
      if (pguidCmdGroup != typeof(VSConstants.VSStd97CmdID).GUID)
        return Next.Exec(pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);

      switch (nCmdId)
      {
        case ReplaceTacticF10:
          DafnyClassifier.DafnyMenuPackage.TacnyMenuProxy.ReplaceOneCall(_tv);
          return VSConstants.S_OK;
        case PeekTacticF9:
          DafnyClassifier.DafnyMenuPackage.TacnyMenuProxy.ShowRot(_tv);
          return VSConstants.S_OK;
        default:
          return Next.Exec(pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
      }
    }

  }

  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("dafny")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class KeyBindingCommandFilterProvider : IVsTextViewCreationListener
  {
    [Import(typeof(IVsEditorAdaptersFactoryService))]
    internal IVsEditorAdaptersFactoryService EditorFactory { get; set; }
    
    public void VsTextViewCreated(IVsTextView tva)
    {
      var tv = EditorFactory.GetWpfTextView(tva);
      if (tv == null) return;

      var cf = new TacticsKeyBindingFilter(tv);
      if (cf.Added) return;

      IOleCommandTarget next;
      var hr = tva.AddCommandFilter(cf, out next);
      if (hr != VSConstants.S_OK) return;

      cf.Added = true;
      if (next != null) cf.Next = next;
    }
  }
}
