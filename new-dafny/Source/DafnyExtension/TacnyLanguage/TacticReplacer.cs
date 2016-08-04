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

namespace DafnyLanguage.TacnyLanguage
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

  public class TacticReplacerProxy : ITacnyMenuProxy
  {
    private readonly IPeekBroker _pb;

    public TacticReplacerProxy(ITextDocumentFactoryService tdf, IServiceProvider isp, IPeekBroker pb) {
      Util.Status = (IVsStatusbar) isp.GetService(typeof(SVsStatusbar));
      Util.Tdf = tdf;
      _pb = pb;
    }
    
    public bool ReplaceOneCall(IWpfTextView atv)
    {
      Contract.Assume(atv != null);
      var caret = atv.Caret.Position.BufferPosition.Position;
      var tra = new TacticReplacerActor(atv.TextBuffer, caret);
      if (tra.LoadStatus != TacticReplaceStatus.Success)  return Util.NotifyOfReplacement(tra.LoadStatus);
      var tedit = atv.TextBuffer.CreateEdit();
      var status = TacticReplaceStatus.TranslatorFail;
      try {
        status = tra.ReplaceSingleTacticCall(tedit);
        if (status == TacticReplaceStatus.Success) { tedit.Apply(); } else { tedit.Dispose(); }
      } catch {  tedit.Dispose(); }
      return Util.NotifyOfReplacement(status);
    }

    public bool ShowRot(IWpfTextView atv)
    {
      Contract.Assume(atv != null);

      string testString;
      var caret = atv.Caret.Position.BufferPosition.Position;
      var tra = new TacticReplacerActor(atv.TextBuffer, caret);
      if (tra.LoadStatus != TacticReplaceStatus.Success) return Util.NotifyOfReplacement(tra.LoadStatus);
      var status = tra.ExpandSingleTacticCall(caret, out testString);
      if (status != TacticReplaceStatus.Success)
        return Util.NotifyOfReplacement(status);
      
      _pb.TriggerPeekSession(new PeekSessionCreationOptions(atv, RotPeekRelationship.SName, null, 0, false, null, false));
      return Util.NotifyOfReplacement(TacticReplaceStatus.Success);
    }

    public bool ReplaceAll(ITextBuffer tb)
    {
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
      return Util.NotifyOfReplacement(replaceStatus);
    }

    public static string GetExpandedForRot(int position, ITextBuffer tb)
    {
      Contract.Assume(tb != null);
      string fullMethod;
      var tra = new TacticReplacerActor(tb, position);
      return tra.ExpandSingleTacticCall(position, out fullMethod)==TacticReplaceStatus.Success ? fullMethod : null;
      //var splitMethod = fullMethod.Split('\n');
      //return splitMethod.Where((t, i) => i >= 2 && i <= splitMethod.Length - 3).Aggregate("", (current, t) => current + t + "\n");
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
  }

  public static class Util
  {
    public static ITextDocumentFactoryService Tdf;
    public static IVsStatusbar Status;

    public static bool LoadAndCheckDocument(ITextBuffer tb, out string filePath)
    {
      Contract.Requires(tb != null);
      ITextDocument doc = null;
      Tdf?.TryGetTextDocument(tb, out doc);
      filePath = doc?.FilePath;
      return !String.IsNullOrEmpty(filePath);
    }

    public static Program GetProgram(ITextBuffer tb, string file, bool resolved) => new TacnyDriver(tb, file).ParseAndTypeCheck(resolved);
    
    public static TacticReplaceStatus GetMemberFromPosition(DefaultClassDecl tld, int position, out MemberDecl member)
    {
      Contract.Requires(tld != null);
      member = (from m in tld.Members
                where m.tok.pos <= position && position <= m.BodyEndTok.pos + 1
                select m).FirstOrDefault();
      return member == null ? TacticReplaceStatus.NoTactic : TacticReplaceStatus.Success;
    }

    public static string StripExtraContentFromExpanded(string expandedTactic)
    {
      var words = new[] { "ghost ", "lemma ", "method ", "function ", "tactic " };
      return words.Aggregate(expandedTactic, RazeFringe);
    }

    public static string RazeFringe(string body, string fringe)
    {
      Contract.Requires(body.Length > fringe.Length);
      return body.Substring(0, fringe.Length) == fringe ? body.Substring(fringe.Length) : body;
    }

    public static bool SameStatement(Statement a, Statement b)
    {
      if (a == null || b == null) return false;
      var sr = new StringWriter();
      var printer = new Printer(sr);
      printer.PrintStatement(a, 0);
      var sa = sr.ToString();
      sr.Flush();
      printer.PrintStatement(b, 0);
      var sb = sr.ToString();
      return sa == sb;
    }

    public static TacticReplaceStatus GetTacticCallAtPosition(Method m, int p, out Tuple<UpdateStmt, int, int> us) {
      us = (from stmt in m?.Body.Body
              let u = stmt as UpdateStmt
              let rhs = u?.Rhss[0] as ExprRhs
              let expr = rhs?.Expr as ApplySuffix
              let name = expr?.Lhs as NameSegment
              where name != null
              let start = expr.tok.pos - name.Name.Length
              let end = rhs.Tok.pos + 3
              where start < p && p < end
              select new Tuple<UpdateStmt, int, int>(stmt as UpdateStmt, start, end))
              .FirstOrDefault();
      return us != null ? TacticReplaceStatus.Success : TacticReplaceStatus.NoTactic;
    }

    public static IEnumerator<Tuple<UpdateStmt, int, int>> GetTacticCallsInMember(Method m) {
      if (m?.Body.Body == null) yield break;
      foreach (var stmt in m.Body.Body)
      {
        var us = stmt as UpdateStmt;
        if (us == null || us.Lhss.Count != 0 || !us.IsGhost) continue;
        Tuple<UpdateStmt, int, int> current;
        if(GetTacticCallAtPosition(m, us.Tok.pos, out current) != TacticReplaceStatus.Success) continue;
        yield return current;
      }
    }

    public static bool NotifyOfReplacement(TacticReplaceStatus t)
    {
      if (Status == null) return false;
      switch (t)
      {
        case TacticReplaceStatus.NoDocumentPersistence:
          Status.SetText("Document must be saved in order to expand tactics.");
          break;
        case TacticReplaceStatus.TranslatorFail:
          Status.SetText("Tacny was unable to expand requested tactics.");
          break;
        case TacticReplaceStatus.NotResolved:
          Status.SetText("File must first be resolvable to expand tactics.");
          break;
        case TacticReplaceStatus.NoTactic:
          Status.SetText("There is no method under the caret that has expandable tactics.");
          break;
        case TacticReplaceStatus.Success:
          Status.SetText("Tactic expanded succesfully.");
          break;
        default:
          throw new Exception("Escaped Switch with " + t);
      }
      return true;
    }
  }

  public class TacticReplacerActor
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
    public TacticReplaceStatus LoadStatus;

    public TacticReplacerActor(ITextBuffer tb, int position = -1)
    {
      Contract.Requires(tb != null);
      string currentFileName;
      LoadStatus = Util.LoadAndCheckDocument(tb, out currentFileName) ? TacticReplaceStatus.Success : TacticReplaceStatus.NoDocumentPersistence;
      if(LoadStatus!=TacticReplaceStatus.Success) return;
      _program = Util.GetProgram(tb, currentFileName, true);
      _unresolvedProgram = Util.GetProgram(tb, currentFileName, false);
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
      var status = Util.GetMemberFromPosition(_tld, position, out _member);
      _tacticCalls = Util.GetTacticCallsInMember(_member as Method);
      return status;
    }

    public TacticReplaceStatus SetTacticCall(int position)
    {
      return Util.GetTacticCallAtPosition(_member as Method, position, out _tacticCall);
    }
    
    public bool NextMemberInTld()
    {
      var isMore = _tldMembers.MoveNext();
      if (!isMore) return false;
      _member = _tldMembers.Current;
      _tacticCalls = Util.GetTacticCallsInMember(_member as Method);
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
      var status = Util.GetTacticCallAtPosition(_member as Method, tacticCallPos, out us);
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
      expandedTactic = Util.StripExtraContentFromExpanded(sr.ToString());
      return !string.IsNullOrEmpty(expandedTactic) ? TacticReplaceStatus.Success : TacticReplaceStatus.NoTactic;
    }
  }
}