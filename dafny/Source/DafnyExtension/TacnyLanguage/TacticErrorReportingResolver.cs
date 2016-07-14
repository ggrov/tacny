using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Bpl = Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage.TacnyLanguage
{
  internal class TacticErrorReportingResolver
  {
    public readonly Bpl.ErrorInformation ErrorInfo;
    public readonly Program Program, TmpProgram;
    public int FailingLine, FailingCol, TacticLine, TacticCol, CallingLine, CallingCol;
    // failing is the line in the tactic that causes the failure
    // tactic is the signature of the tactic
    // calling is the line that makes a call tot he tactic in the original method
    // TODO move comments like this into documentation instead

    public bool FoundFailing => !(FailingLine == -1 || FailingCol == -1);
    public bool FoundTactic => !(TacticLine == -1 || TacticCol == -1);
    public bool FoundCalling => !(CallingLine == -1 || CallingCol == -1);

    public TacticErrorReportingResolver(Bpl.ErrorInformation errorInfo, Program program, Program tmpProgram)
    {
      Contract.Requires<NullReferenceException>(errorInfo != null);
      Contract.Requires<NullReferenceException>(program != null);
      //Contract.Requires<NullReferenceException>(tmpProgram != null); //TODO figure out the whole tmp program situation
      ErrorInfo = errorInfo;
      Program = program;
      TmpProgram = tmpProgram;
      FailingLine = FailingCol = TacticLine = TacticCol = CallingLine = CallingCol = -1;
    }

    public void ResolveCorrectLocations()
    {
      var matches = Regex.Match(ErrorInfo.ImplementationName, @".*\$\$.*\..*\.(.*)");
      if (matches.Groups.Count != 2) return;
      var methodName = matches.Groups[1].Value;

      var module = (DefaultClassDecl)Program.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
      var callingMethod = module?.Members.FirstOrDefault(x => x.CompileName == methodName) as Method;
      if (callingMethod == null) return;

      var bodyStatements = callingMethod.Body.SubStatements;
      var tacticCall = (from updater in bodyStatements.OfType<UpdateStmt>()
                        where updater.IsGhost && updater.Lhss.Count == 0
                        select (ExprRhs)updater.Rhss[0]).FirstOrDefault();
      if (tacticCall == null) return;
      
      var tacticName = ((NameSegment)((ApplySuffix)tacticCall.Expr).Lhs).Name;
      CallingLine = tacticCall.Tok.line;
      CallingCol = tacticCall.Tok.col;
      
      var tactic = (Tactic)module.Members.FirstOrDefault(x => x.CompileName == tacticName);
      if (tactic == null) return;
      TacticLine = tactic.BodyStartTok.line;
      TacticCol = tactic.BodyStartTok.col;
      var tacE = tactic.BodyEndTok.line;
      
      var tacticStatements = tactic.Body.SubStatements;
      var offsetToFailure = 0; //TODO obviously this is just temporary
      var failing = (from stmt in tacticStatements.ToArray()
                     where stmt.Tok.line == TacticLine + Math.Min(offsetToFailure, tacE)
                     select stmt.Tok).FirstOrDefault();
      if (failing == null) return;

      FailingCol = failing.col;
      FailingLine = failing.line;
    }

    public void AddTacticErrors(ResolverTagger errorListHolder, ITextSnapshot snap, string requestId, string file)
    {
      Contract.Requires<NullReferenceException>(errorListHolder != null);
      Contract.Requires<NullReferenceException>(snap != null);
      Contract.Requires<NullReferenceException>(!string.IsNullOrEmpty(requestId));
      Contract.Requires<NullReferenceException>(!string.IsNullOrEmpty(file));

      if (!FoundCalling) return;
      errorListHolder.AddError(
             new DafnyError(file, CallingLine - 1, CallingCol - 1,
               ErrorCategory.TacticError, ErrorInfo.FullMsg, snap, true, ""),
             "$$program_tactics$$", requestId);
      
      if (!FoundTactic) return;
      errorListHolder.AddError(
             new DafnyError(file, TacticLine - 1, TacticCol - 1,
               ErrorCategory.TacticError, ErrorInfo.FullMsg, snap, true, ""),
             "$$program_tactics$$", requestId);

      if (!FoundFailing) return;
      errorListHolder.AddError(
             new DafnyError(file, FailingLine - 1, FailingCol - 1,
               ErrorCategory.TacticError, ErrorInfo.FullMsg, snap, true, ""),
             "$$program_tactics$$", requestId);
    }
  }
}
