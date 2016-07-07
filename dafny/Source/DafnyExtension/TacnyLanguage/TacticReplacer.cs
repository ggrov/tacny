using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Tacny;

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
    internal IServiceProvider ServiceProvider { get; set; }

    [Import]
    internal IViewTagAggregatorFactoryService Vtafs { get; set; }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      var textView = EditorAdaptersFactory.GetWpfTextView(textViewAdapter);
      if (textView == null) throw new Exception("Failed to access WpfTextView");
      var navigator = TextStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
      var ta = Vtafs.CreateTagAggregator<DafnyTokenTag>(textView);
      var statusbar = (IVsStatusbar)ServiceProvider.GetService(typeof(SVsStatusbar));
      
      var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
      if (vsShell == null) throw new NullReferenceException("VS Shell failed to Load");
      IVsPackage shellPack;
      var packToLoad = new Guid("e1baf989-88a6-4acf-8d97-e0dc243476aa");
      if (vsShell.LoadPackage(ref packToLoad, out shellPack) != VSConstants.S_OK)
        throw new NullReferenceException("Dafny Menu failed to Load");
      var dafnyMenuPack = (DafnyMenuPackage)shellPack;
      dafnyMenuPack.TacnyMenuProxy = new TacticReplacerProxy(new TacticReplacerCommandFilter(textView, navigator, statusbar, Tdf, ta));
    }
  }

  public class TacticReplacerProxy : ITacnyMenuProxy
  {
    private readonly TacticReplacerCommandFilter _trcf;

    public TacticReplacerProxy(TacticReplacerCommandFilter trcf)
    {
      _trcf = trcf;
    }
    
    public void Exec()
    {
      _trcf.Exec();
    }
  }

  public class TacticReplacerCommandFilter
  {
    private readonly IWpfTextView _tv;
    private readonly ITextStructureNavigator _tsn;
    private readonly ITextDocumentFactoryService _tdf;
    private readonly ITagAggregator<DafnyTokenTag> _ta;
    private readonly IVsStatusbar _status;
    private ITextDocument _document;

    public TacticReplacerCommandFilter(IWpfTextView textView, ITextStructureNavigator tsn, IVsStatusbar sb,
      ITextDocumentFactoryService tdf, ITagAggregator<DafnyTokenTag> ta)
    {
      _tv = textView;
      _tsn = tsn;
      _tdf = tdf;
      LoadAndCheckDocument();
      _ta = ta;
      _status = sb;
    }

    public void Exec()
    {
      var status = ExecuteReplacement();
      NotifyOfReplacement(status);
    }
    

    private void NotifyOfReplacement(TacticReplaceStatus t)
    {
      switch (t)
      {
        case TacticReplaceStatus.NoDocumentPersistence:
          _status.SetText("Document must be saved in order to expand tactics.");
          break;
        case TacticReplaceStatus.DriverFail:
          _status.SetText("Tacny was unable to expand requested tactics.");
          break;
        case TacticReplaceStatus.NotResolved:
          _status.SetText("File must first be resolvable to expand tactics.");
          break;
        case TacticReplaceStatus.NoTactic:
          _status.SetText("There is no method signature name containing under the caret that has expandable tactics.");
          break;
        case TacticReplaceStatus.Success:
          _status.SetText("Tactic expanded succesfully.");
          break;
        default:
          throw new Exception("Escaped Switch with "+t);
      }
    }

    internal enum TacticReplaceStatus
    {
      Success,
      NoDocumentPersistence,
      NoTactic,
      NotResolved,
      DriverFail
    }
    
    private bool LoadAndCheckDocument()
    {
      _tdf.TryGetTextDocument(_tv.TextBuffer, out _document);
      return !string.IsNullOrEmpty(_document?.FilePath);
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

      string expandedTactic;
      var expandedStatus = GetExpandedTactic(startingWord, out expandedTactic);
      if (expandedStatus != TacticReplaceStatus.Success)
        return expandedStatus;

      var tedit = _tv.TextBuffer.CreateEdit();
      tedit.Replace(startOfBlock, lengthOfBlock, expandedTactic);
      tedit.Apply();
      return TacticReplaceStatus.Success;
    }
    
    private TacticReplaceStatus GetExpandedTactic(string startingWord, out string expandedTactic)
    {
      expandedTactic = "";
      if (!LoadAndCheckDocument()) return TacticReplaceStatus.NoDocumentPersistence;
      var driver = new DafnyDriver(_tv.TextBuffer, _document.FilePath);

      driver.ProcessResolution(true, true);
      var program = driver.Program;
      if (program == null) return TacticReplaceStatus.NotResolved;
      
      MemberDecl member = null;
      foreach (var def in program.DefaultModuleDef.TopLevelDecls)
      {
        if (!(def is ClassDecl)) continue;
        var c = (ClassDecl) def;
        member = c.Members.FirstOrDefault(x => x.Name == startingWord);
        if (member != null) break;
      }
      if (member == null) return TacticReplaceStatus.NotResolved;
      if (!member.CallsTactic) return TacticReplaceStatus.NoTactic;
      var status = TacticReplaceStatus.Success;
      var evaluatedMember = Interpreter.FindAndApplyTactic(program, member, errorInfo => {status = TacticReplaceStatus.DriverFail;});
      if (evaluatedMember == null || status != TacticReplaceStatus.Success) return TacticReplaceStatus.DriverFail;

      var sr = new StringWriter();
      var printer = new Printer(sr);
      printer.PrintMembers(new List<MemberDecl> { evaluatedMember }, 0, program.FullName);
      expandedTactic = sr.ToString();
      return TacticReplaceStatus.Success;
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
        if (IsSnapSpanInComment(_tv.GetTextElementSpan(advancingSnapshot))) continue;
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
        "predicate",
        "tactic"
      };

      var currentSubling = _tsn.GetSpanOfNextSibling(startingWordPosition);
      
      var next = _tsn.GetSpanOfNextSibling(currentSubling).GetText();
      if (next != "(" && next != "(){" && next != "()") return -1;

      var prev = _tsn.GetSpanOfPreviousSibling(currentSubling);
      var s = prev.GetText();
      if (!listOfStarters.Contains(s)) return -1;

      var startingPos = prev.Start;
      var ghostPrev = _tsn.GetSpanOfPreviousSibling(prev);
      return ghostPrev.GetText() == "ghost" ? ghostPrev.Start : startingPos;
    }
    
  }
}