using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Text;
using Tacny;

namespace DafnyLanguage.Refactoring
{
  internal static class RefactoringUtil
  {
    public static ITextDocumentFactoryService Tdf;

    public static DefaultClassDecl GetTld(Program p) => 
      (from tld in p?.DefaultModuleDef.TopLevelDecls
        let test = tld as DefaultClassDecl
        where test != null
        select test).FirstOrDefault();

    public static SnapshotSpan GetRangeOfMember(ITextSnapshot snap, MemberDecl m) =>
      new SnapshotSpan(snap, m.tok.pos, m.BodyEndTok.pos - m.tok.pos);

    public static Program GetNewProgram(ITextBuffer tb) {
      string file;
      LoadAndCheckDocument(tb, out file);
      var driver = new TacnyDriver(tb, file);
      return driver.ReParse(false);
    }

    public static bool LoadAndCheckDocument(ITextBuffer tb, out string filePath) {
      Contract.Requires(tb != null);
      ITextDocument doc = null;
      Tdf?.TryGetTextDocument(tb, out doc);
      filePath = doc?.FilePath;
      return !string.IsNullOrEmpty(filePath);
    }
    
    public static bool GetExistingProgram(ITextBuffer tb, out Program p) {
      p = null;
      string file;
      return LoadAndCheckDocument(tb, out file) && TacnyDriver.GetExistingProgramFromBuffer(tb, out p);
    }

    public static bool ProgramIsVerified(ITextBuffer tb) {
      if (!tb.Properties.ContainsProperty(typeof(ResolverTagger))) return false;
      var rt = tb.Properties.GetProperty(typeof(ResolverTagger)) as ResolverTagger;
      if (rt == null) return false;
      return !rt.AllErrors.Any(error => {
          switch (error.Category) {
            case ErrorCategory.ParseError:
            case ErrorCategory.ResolveError:
            case ErrorCategory.VerificationError:
            case ErrorCategory.InternalError:
            case ErrorCategory.TacticError:
            case ErrorCategory.ProcessError:
              return true;
            case ErrorCategory.ParseWarning:
            case ErrorCategory.ResolveWarning:
            case ErrorCategory.AuxInformation:
              return false;
            default:
              throw new tcce.UnreachableException();
          }
      });
    }

    public static Program GetReparsedProgram(ITextBuffer tb, string file, bool resolved) => new TacnyDriver(tb, file).ReParse(resolved);

    public static TacticReplaceStatus GetMemberFromPosition(DefaultClassDecl tld, int position, out MemberDecl member) {
      Contract.Requires(tld != null);
      member = GetMemberFromPosition(tld, position);
      return member == null ? TacticReplaceStatus.NoTactic : TacticReplaceStatus.Success;
    }

    public static MemberDecl GetMemberFromPosition(DefaultClassDecl tld, int position) => 
      (from m in tld.Members where m.tok.pos <= position && position <= m.BodyEndTok.pos + 1 select m).FirstOrDefault();
    
    public static string StripExtraContentFromExpanded(string expandedTactic) {
      var words = new[] { "ghost ", "lemma ", "method ", "function ", "tactic " };
      return words.Aggregate(expandedTactic, RazeFringe);
    }

    public static string RazeFringe(string body, string fringe) {
      Contract.Requires(body.Length > fringe.Length);
      return body.Substring(0, fringe.Length) == fringe ? body.Substring(fringe.Length) : body;
    }

    public static bool TokenEquals(Microsoft.Boogie.IToken t, Microsoft.Boogie.IToken u) {
      return t.IsValid == u.IsValid && t.col == u.col && t.filename == u.filename
             && t.kind == u.kind && t.line == u.line && t.pos == u.pos && t.val == u.val;
    }
    
    public static TacticReplaceStatus GetTacticCallAtPosition(Method m, int p, out Tuple<UpdateStmt, int, int> us) {
      try {
        us = (from stmt in m?.Body?.Body
              let u = stmt as UpdateStmt
              let rhs = u?.Rhss[0] as ExprRhs
              let expr = rhs?.Expr as ApplySuffix
              let name = expr?.Lhs as NameSegment
              where name != null
              let start = expr.tok.pos - name.Name.Length
              let end = stmt.EndTok.pos + 1
              where start <= p && p <= end
              select new Tuple<UpdateStmt, int, int>(stmt as UpdateStmt, start, end))
          .FirstOrDefault();
      } catch (ArgumentNullException) { us = null; }
      return us != null ? TacticReplaceStatus.Success : TacticReplaceStatus.NoTactic;
    }

    public static IEnumerator<Tuple<UpdateStmt, int, int>> GetTacticCallsInMember(Method m) {
      if (m?.Body.Body == null) yield break;
      foreach (var stmt in m.Body.Body) {
        var us = stmt as UpdateStmt;
        if (us == null || us.Lhss.Count != 0 || !us.IsGhost) continue;
        Tuple<UpdateStmt, int, int> current;
        if (GetTacticCallAtPosition(m, us.Tok.pos, out current) != TacticReplaceStatus.Success) continue;
        yield return current;
      }
    }
  }
}