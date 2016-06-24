using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Printer = Microsoft.Dafny.Printer;

namespace DafnyLanguage.TacnyLanguage
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("dafny")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class TacticReplacerFilterProvider : IVsTextViewCreationListener
  {
    [Import(typeof(IVsEditorAdaptersFactoryService))]
    internal IVsEditorAdaptersFactoryService EditorAdaptersFactory { get; set; }

    [Import(typeof(ITextDocumentFactoryService))]
    internal ITextDocumentFactoryService Tdf;

    [Import(typeof(ITextStructureNavigatorSelectorService))]
    internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

    [Import(typeof(SVsServiceProvider))]
    internal System.IServiceProvider ServiceProvider { get; set; }

    [Import]
    internal IViewTagAggregatorFactoryService Vtafs { get; set; }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      var textView = EditorAdaptersFactory.GetWpfTextView(textViewAdapter);
      if (textView == null) return;
      var navigator = TextStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
      var ta = Vtafs.CreateTagAggregator<DafnyTokenTag>(textView);
      AddCommandFilter(textViewAdapter, new TacticReplacerCommandFilter(textView, navigator, ServiceProvider, Tdf, ta));
    }

    private static void AddCommandFilter(IVsTextView viewAdapter, TacticReplacerCommandFilter commandFilter)
    {
      IOleCommandTarget next;
      if (VSConstants.S_OK != viewAdapter.AddCommandFilter(commandFilter, out next)) return;
      if (next != null)
        commandFilter.NextCmdTarget = next;
    }
  }

  internal class TacticReplacerCommandFilter : IOleCommandTarget
  {
    internal IOleCommandTarget NextCmdTarget;
    private readonly IWpfTextView _tv;
    private readonly ITextStructureNavigator _tsn;
    private readonly System.IServiceProvider _isp;
    private readonly ITextDocumentFactoryService _tdf;
    private readonly ITagAggregator<DafnyTokenTag> _ta;
    private ITextDocument _document;

    public TacticReplacerCommandFilter(IWpfTextView textView, ITextStructureNavigator tsn, System.IServiceProvider sp,
      ITextDocumentFactoryService tdf, ITagAggregator<DafnyTokenTag> ta)
    {
      _tv = textView;
      _tsn = tsn;
      _isp = sp;
      _tdf = tdf;
      LoadAndCheckDocument();
      _ta = ta;
    }
    
    public int Exec(ref Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
      var status = TacticReplaceStatus.NoTactic;
      if (VsShellUtilities.IsInAutomationFunction(_isp))
        return NextCmdTarget.Exec(pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);

      if (pguidCmdGroup == VSConstants.VSStd2K && nCmdId == (uint) VSConstants.VSStd2KCmdID.TYPECHAR)
      {
        var typedChar = (char) (ushort) Marshal.GetObjectForNativeVariant(pvaIn);
        if (typedChar.Equals('#'))
        {
          status = ExecuteReplacement();
        }
      }
      return status == TacticReplaceStatus.Success
        ? VSConstants.S_OK
        : NextCmdTarget.Exec(pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
    }

    internal enum TacticReplaceStatus
    {
      Success,
      NoTactic
    }
    
    private bool LoadAndCheckDocument(bool checkDirty = false)
    {
      _tdf.TryGetTextDocument(_tv.TextBuffer, out _document);
      return checkDirty ? _document!=null&&!_document.IsDirty : _document != null;
    }

    private bool IsSnapSpanInComment(SnapshotSpan s)
    {
      var tags = _ta.GetTags(s);
      var foundComment = tags.FirstOrDefault(x => x.Tag.Kind == DafnyTokenKind.Comment);
      return foundComment!=null;
    }

    private TacticReplaceStatus ExecuteReplacement()
    {
      var startingWordPosition = _tv.GetTextElementSpan(_tv.Caret.Position.BufferPosition);
      if (IsSnapSpanInComment(startingWordPosition)) return TacticReplaceStatus.NoTactic;

      var startingWordPoint = _tv.Caret.Position.Point.GetPoint(_tv.TextBuffer, PositionAffinity.Predecessor);
      if (startingWordPoint == null) return TacticReplaceStatus.NoTactic;
      var startingWord = _tv.TextSnapshot.GetText(_tsn.GetExtentOfWord(startingWordPoint.Value).Span);

      var startOfBlock = StartOfBlock(startingWordPosition);
      if (startOfBlock == -1) return TacticReplaceStatus.NoTactic;
      var lengthOfBlock = LengthOfBlock(startOfBlock);

      var expandedTactic = GetExpandedTactic(startingWord);
      if (expandedTactic == "") return TacticReplaceStatus.NoTactic;

      var tedit = _tv.TextBuffer.CreateEdit();
      tedit.Replace(startOfBlock, lengthOfBlock, expandedTactic);
      tedit.Apply();
      return TacticReplaceStatus.Success;
    }


    private string GetExpandedTactic(string startingWord)
    {
      if (!LoadAndCheckDocument(true)) return "";
      var driver = new DafnyDriver(_tv.TextBuffer, _document.FilePath);

      driver.ProcessResolution(true);
      var ast = driver.Program;
      if (ast == null) return "";

      MemberDecl member = null;
      foreach (var def in ast.DefaultModuleDef.TopLevelDecls)
      {
        if (!(def is ClassDecl)) continue;
        var c = (ClassDecl) def;
        member = c.Members.FirstOrDefault(x => x.Name == startingWord);
        if (member != null) break;
      }
      var evaluatedMember = Tacny.Interpreter.FindAndApplyTactic(ast, member);

      var sr = new StringWriter();
      var printer = new Printer(sr);
      printer.PrintMembers(new List<MemberDecl> {evaluatedMember}, 0, ast.FullName);
      return sr.ToString();
    }

    private int LengthOfBlock(int startingPoint)
    {
      var advancingPoint = startingPoint;
      var bodyOpened = false;
      var braceDepth = 0;
      while (braceDepth > 0 || !bodyOpened)
      {
        var advancingSnapshot = new SnapshotPoint(_tv.TextSnapshot, ++advancingPoint);
        var currentChar = advancingSnapshot.GetChar();
        switch (currentChar)
        {
          case '{':
            braceDepth++;
            bodyOpened = true;
            break;
          case '}':
            braceDepth--;
            break;
        }
      }
      return advancingPoint - startingPoint + 1;
    }

    private int StartOfBlock(SnapshotSpan startingWordPosition)
    {
      var listOfStarters = new[]
      {
        "function",
        "lemma",
        "method",
        "predicate"
      };

      var currentSubling = _tsn.GetSpanOfNextSibling(startingWordPosition);
      
      var next = _tsn.GetSpanOfNextSibling(currentSubling).GetText();
      if (next != "(" && next != "(){") return -1;

      var prev = _tsn.GetSpanOfPreviousSibling(currentSubling);
      var s = prev.GetText();
      if (!listOfStarters.Contains(s)) return -1;

      var startingPos = prev.Start;
      var ghostPrev = _tsn.GetSpanOfPreviousSibling(prev);
      return ghostPrev.GetText() == "ghost" ? ghostPrev.Start : startingPos;
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
      return NextCmdTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }
  }
}