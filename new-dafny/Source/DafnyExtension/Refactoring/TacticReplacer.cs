using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Printer = Microsoft.Dafny.Printer;
using Program = Microsoft.Dafny.Program;

namespace DafnyLanguage.Refactoring
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("dafny")]
  [Name("TacticReplacerProvider")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class TacticReplacerProvider : IVsTextViewCreationListener
  {
    [Import(typeof(ITextDocumentFactoryService))]
    internal ITextDocumentFactoryService Tdf { get; set; }
    
    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider { get; set; }

    [Import]
    internal IPeekBroker Pb { get; set; }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
      if (vsShell == null) throw new NullReferenceException("VS Shell failed to Load");
      IVsPackage shellPack;
      var packToLoad = new Guid("e1baf989-88a6-4acf-8d97-e0dc243476aa");
      if (vsShell.LoadPackage(ref packToLoad, out shellPack) != VSConstants.S_OK)
        throw new NullReferenceException("Dafny Menu failed to Load");
      var dafnyMenuPack = (DafnyMenuPackage)shellPack;
      dafnyMenuPack.TacnyMenuProxy = new TacticReplacerProxy(Tdf, ServiceProvider, Pb);
    }
  }

  public enum TacticReplaceStatus
  {
    Success,
    NoDocumentPersistence,
    NoTactic,
    NotResolved,
    TranslatorFail
  }

  internal class ActivePeekSessionData
  {
    public Action<string> Updater { get; set; }
    public ITrackingPoint TriggerPoint { get; set; }
    public string ExpandedTactic { get; set; }
    public string ActiveTactic { get; set; }
  }

  internal class TacticReplacerProxy : ITacnyMenuProxy
  {
    private readonly IPeekBroker _pb;
    private readonly Dictionary<string, ActivePeekSessionData> _activePeekSession;
    private static IVsStatusbar _status;

    public TacticReplacerProxy(ITextDocumentFactoryService tdf, IServiceProvider isp, IPeekBroker pb) {
      _status = (IVsStatusbar) isp.GetService(typeof(SVsStatusbar));
      RefactoringUtil.Tdf = tdf;
      _pb = pb;
      _activePeekSession = new Dictionary<string, ActivePeekSessionData>();
    }
    
    public bool ReplaceOneCall(IWpfTextView atv)
    {
      Contract.Assume(atv != null);
      var caret = atv.Caret.Position.BufferPosition.Position;
      var tra = new TacticReplacerActor(atv.TextBuffer, caret);
      if (tra.LoadStatus != TacticReplaceStatus.Success)  return NotifyOfReplacement(tra.LoadStatus);
      var tedit = atv.TextBuffer.CreateEdit();
      var status = TacticReplaceStatus.TranslatorFail;
      try {
        status = tra.ReplaceSingleTacticCall(tedit);
        if (status == TacticReplaceStatus.Success) { tedit.Apply(); } else { tedit.Dispose(); }
      } catch {  tedit.Dispose(); }
      return NotifyOfReplacement(status);
    }

    public bool ShowRot(IWpfTextView atv)
    {
      Contract.Assume(atv != null);

      string expanded;
      var caret = atv.Caret.Position.BufferPosition.Position;
      var triggerPoint = atv.TextBuffer.CurrentSnapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive);
      var tra = new TacticReplacerActor(atv.TextBuffer, caret);
      if (tra.LoadStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(tra.LoadStatus);
      var status = tra.ExpandSingleTacticCall(caret, out expanded);
      if (status != TacticReplaceStatus.Success)
        return NotifyOfReplacement(status);
      
      string file;
      var fileLoaded = RefactoringUtil.LoadAndCheckDocument(atv.TextBuffer, out file);
      if (!fileLoaded) return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      
      var session = _pb.CreatePeekSession(new PeekSessionCreationOptions(atv, RotPeekRelationship.SName, triggerPoint, 0, false, null, false));
      _activePeekSession.Remove(file);
      _activePeekSession.Add(file, new ActivePeekSessionData {
        TriggerPoint = triggerPoint,
        ExpandedTactic = expanded,
        ActiveTactic = tra.ActiveTacticName
      });
      session.Start();
      return NotifyOfReplacement(TacticReplaceStatus.Success);
    }
    
    public bool ReplaceAll(ITextBuffer tb) {
      Contract.Assume(tb != null);

      var tra = new TacticReplacerActor(tb);
      var isMoreMembers = tra.NextMemberInTld();
      var replaceStatus = TacticReplaceStatus.Success;
      var tedit = tb.CreateEdit();
      try
      {
        while (isMoreMembers && (replaceStatus == TacticReplaceStatus.Success || replaceStatus == TacticReplaceStatus.NoTactic))
        {
          var isMoreTactics = tra.NextTacticCallInMember();
          while (isMoreTactics && (replaceStatus == TacticReplaceStatus.Success || replaceStatus == TacticReplaceStatus.NoTactic))
          {
            replaceStatus = tra.ReplaceSingleTacticCall(tedit);
            isMoreTactics = tra.NextTacticCallInMember();
          }
          isMoreMembers = tra.NextMemberInTld();
        }

        if(replaceStatus==TacticReplaceStatus.Success || replaceStatus == TacticReplaceStatus.NoTactic)
          { tedit.Apply();} else { tedit.Dispose();}
      } catch { tedit.Dispose(); }
      return NotifyOfReplacement(replaceStatus);
    }

    public void AddUpdaterForRot(IPeekSession session, Action<string> recalculate)
    {
      string file;
      var fileLoaded = RefactoringUtil.LoadAndCheckDocument(session.TextView.TextBuffer, out file);
      if (fileLoaded && _activePeekSession!=null && _activePeekSession.ContainsKey(file))
        _activePeekSession[file].Updater = recalculate;
    }

    public bool ClearPeekSession(IPeekSession session)
    {
      string file;
      return RefactoringUtil.LoadAndCheckDocument(session.TextView.TextBuffer, out file) && _activePeekSession.Remove(file);
    }

    public bool UpdateRot(string file, ITextSnapshot snapshot)
    {
      if (String.IsNullOrEmpty(file) || _activePeekSession==null || !_activePeekSession.ContainsKey(file)) return false;
      var session = _activePeekSession[file];
      string expanded;
      var position = session.TriggerPoint.GetPosition(snapshot);
      var tra = new TacticReplacerActor(snapshot.TextBuffer, position);
      if(tra.ActiveTacticName!=session.ActiveTactic) session.Updater?.Invoke(null);
      if (tra.LoadStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(tra.LoadStatus);
      var status = tra.ExpandSingleTacticCall(position, out expanded);
      if (status != TacticReplaceStatus.Success)
        return NotifyOfReplacement(status);
      session.ExpandedTactic = expanded;
      session.Updater?.Invoke(expanded);
      return true;
    }

    public Tuple<string, string> GetExpandedForPeekSession(IPeekSession session)
    {
      string file;
      var status = RefactoringUtil.LoadAndCheckDocument(session.TextView.TextBuffer, out file);
      if(!status) return null;
      var storedSessionData = _activePeekSession[file];
      return new Tuple<string, string>(storedSessionData.ExpandedTactic, storedSessionData.ActiveTactic);
    }
    
    public static string GetExpandedForPreview(int position, ITextBuffer buffer, ref SnapshotSpan methodName)
    {
      Contract.Assume(buffer != null);
      var tra = new TacticReplacerActor(buffer, position);
      if (!tra.MemberReady) return null;

      methodName = new SnapshotSpan(buffer.CurrentSnapshot, tra.MemberNameStart, tra.MemberName.Length);
      string expanded;
      tra.ExpandTactic(out expanded);
      return expanded;
    }

    public static bool NotifyOfReplacement(TacticReplaceStatus t)
    {
      if (_status == null) return false;
      switch (t)
      {
        case TacticReplaceStatus.NoDocumentPersistence:
          _status.SetText("Document must be saved in order to expand tactics.");
          break;
        case TacticReplaceStatus.TranslatorFail:
          _status.SetText("Tacny was unable to expand requested tactics.");
          break;
        case TacticReplaceStatus.NotResolved:
          _status.SetText("File must first be resolvable to expand tactics.");
          break;
        case TacticReplaceStatus.NoTactic:
          _status.SetText("There is no method under the caret that has expandable tactics.");
          break;
        case TacticReplaceStatus.Success:
          _status.SetText("Tactic expanded succesfully.");
          break;
        default:
          throw new Exception("Escaped Switch with " + t);
      }
      return true;
    }
  }

  internal class TacticReplacerActor
  {
    private readonly Program _program, _unresolvedProgram;
    private readonly DefaultClassDecl _tld;
    private readonly IEnumerator<MemberDecl> _tldMembers;
    private MemberDecl _member;
    private Tuple<UpdateStmt, int, int> _tacticCall;
    private IEnumerator<Tuple<UpdateStmt, int, int>> _tacticCalls;

    public int MemberBodyStart => _member.BodyStartTok.pos;
    public int MemberNameStart => _member.tok.pos;
    public string MemberName => _member.CompileName;
    public bool MemberReady => _member!=null && _member.CallsTactic;
    public string ActiveTacticName
      => ((NameSegment) ((ApplySuffix) ((ExprRhs) _tacticCall.Item1.Rhss[0]).Expr).Lhs).Name;
    public TacticReplaceStatus LoadStatus;

    public TacticReplacerActor(ITextBuffer tb, int position = -1)
    {
      Contract.Requires(tb != null);
      string currentFileName;
      LoadStatus = RefactoringUtil.LoadAndCheckDocument(tb, out currentFileName) ? TacticReplaceStatus.Success : TacticReplaceStatus.NoDocumentPersistence;
      if(LoadStatus!=TacticReplaceStatus.Success) return;
      _program = RefactoringUtil.GetReparsedProgram(tb, currentFileName, true);
      _unresolvedProgram = RefactoringUtil.GetReparsedProgram(tb, currentFileName, false);
      _tld = (DefaultClassDecl)_program?.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
      _tldMembers = _tld?.Members.GetEnumerator();
      LoadStatus = _tld != null ? TacticReplaceStatus.Success : TacticReplaceStatus.NotResolved;
      if (LoadStatus != TacticReplaceStatus.Success) return;
      if (position == -1) return;
      SetMember(position);
      SetTacticCall(position);
    }

    public TacticReplaceStatus SetMember(int position)
    {
      var status = RefactoringUtil.GetMemberFromPosition(_tld, position, out _member);
      _tacticCalls = RefactoringUtil.GetTacticCallsInMember(_member as Method);
      return status;
    }

    public TacticReplaceStatus SetTacticCall(int position)
    {
      return RefactoringUtil.GetTacticCallAtPosition(_member as Method, position, out _tacticCall);
    }
    
    public bool NextMemberInTld()
    {
      var isMore = _tldMembers.MoveNext();
      if (!isMore) return false;
      _member = _tldMembers.Current;
      _tacticCalls = RefactoringUtil.GetTacticCallsInMember(_member as Method);
      return true;
    }

    public bool NextTacticCallInMember()
    {
      var isMore = _tacticCalls.MoveNext();
      if (isMore) _tacticCall = _tacticCalls.Current;
      return isMore;
    }

    public TacticReplaceStatus ReplaceSingleTacticCall(ITextEdit tedit)
    {
      Contract.Requires(tedit != null);
      if (!MemberReady || _tacticCall==null) return TacticReplaceStatus.NoTactic;
      
      string expanded;
      var expandedStatus = ExpandSingleTacticCall(_tacticCall.Item1, out expanded);
      if (expandedStatus != TacticReplaceStatus.Success)
        return expandedStatus;
      
      tedit.Replace(_tacticCall.Item2, _tacticCall.Item3 - _tacticCall.Item2, expanded);
      return TacticReplaceStatus.Success;
    }

    public TacticReplaceStatus ExpandSingleTacticCall(int tacticCallPos, out string expanded)
    {
      expanded = "";
      Tuple<UpdateStmt, int, int> us;
      var status = RefactoringUtil.GetTacticCallAtPosition(_member as Method, tacticCallPos, out us);
      return status==TacticReplaceStatus.Success ? ExpandSingleTacticCall(us.Item1, out expanded) : status;
    }

    private TacticReplaceStatus ExpandSingleTacticCall(UpdateStmt us, out string expanded)
    {
      expanded = "";
      var status = TacticReplaceStatus.Success;
      var newStmts = Tacny.Interpreter.FindSingleTactic(_program, _member, us,
        errorInfo => { status = TacticReplaceStatus.TranslatorFail; }, _unresolvedProgram);
      if (status != TacticReplaceStatus.Success) return status;
      if (newStmts == null) return TacticReplaceStatus.NoTactic;

      var sr = new StringWriter();
      var pr = new Printer(sr);
      for(var i = 0; i < newStmts.Count-1; i++)
      {
        pr.PrintStatement(newStmts[i], 4);
        sr.WriteLine();
      }
      pr.PrintStatement(newStmts[newStmts.Count-1], 4);
      expanded += sr;
      return TacticReplaceStatus.Success;
    }

    public TacticReplaceStatus ExpandTactic(out string expandedTactic)
    {
      expandedTactic = null;
      if (!MemberReady) return TacticReplaceStatus.NoTactic;

      var status = TacticReplaceStatus.Success;
      var evaluatedMember = Tacny.Interpreter.FindAndApplyTactic(_program, _member, errorInfo => { status = TacticReplaceStatus.TranslatorFail; }, _unresolvedProgram);
      if (evaluatedMember == null || status != TacticReplaceStatus.Success) return TacticReplaceStatus.TranslatorFail;

      var sr = new StringWriter();
      var printer = new Printer(sr);
      printer.PrintMembers(new List<MemberDecl> { evaluatedMember }, 0, _program.FullName);
      expandedTactic = RefactoringUtil.StripExtraContentFromExpanded(sr.ToString());
      return !string.IsNullOrEmpty(expandedTactic) ? TacticReplaceStatus.Success : TacticReplaceStatus.NoTactic;
    }
  }
}