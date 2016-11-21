using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Text;
using Tacny;
using Bpl = Microsoft.Boogie;

namespace DafnyLanguage.Refactoring
{
  internal class TacticErrorResolutionException : Exception
  {
    public TacticErrorResolutionException(string msg) : base(msg){}
  }
  internal class TacticErrorReportingResolver
  {
    private readonly Bpl.Token _errTok;
    private readonly UpdateStmt _tacticCall;
    private readonly Tactic _activeTactic;
    private readonly MemberDecl _callingMember;
    private readonly MemberDecl _tmpFailingMember;
    private readonly string _errMessage, _implTargetName;
    public int FailingLine, FailingCol, TacticLine, TacticCol, CallingLine, CallingCol;

    public bool FoundFailing => !(FailingLine == -1 || FailingCol == -1);
    public bool FoundTactic => !(TacticLine == -1 || TacticCol == -1);
    public bool FoundCalling => !(CallingLine == -1 || CallingCol == -1);

    public TacticErrorReportingResolver(CompoundErrorInformation errorInfo)
    {
      Contract.Ensures(_errTok != null);
      Contract.Ensures(_errMessage != null);
      Contract.Ensures(_tacticCall != null);
      Contract.Ensures(_activeTactic != null);
      Contract.Ensures(_callingMember != null);
      Contract.Ensures(_tmpFailingMember != null);
      Contract.Ensures(!string.IsNullOrEmpty(_implTargetName));
      var proofState = errorInfo.S;
      var tmpProgram = ((CompoundErrorInformation)errorInfo.E).P;
      var innerError = ((CompoundErrorInformation)errorInfo.E).E;
      var tmpModule = (DefaultClassDecl)tmpProgram.DefaultModuleDef.TopLevelDecls.FirstOrDefault(x => x.CompileName== "__default");

      var tok = innerError.Tok as NestedToken;
      _errTok = tok != null ? (Bpl.Token) tok.Inner : (Bpl.Token) innerError.Tok;
      _errMessage = innerError.FullMsg;

      _implTargetName = MethodNameFromImpl(innerError.ImplementationName);
      _tacticCall = proofState.TacticApplication;
      _activeTactic = proofState.GetTactic(_tacticCall) as Tactic;

      _callingMember = proofState.TargetMethod;
      _tmpFailingMember = tmpModule?.Members.FirstOrDefault(x => x.CompileName == _implTargetName);

      FailingLine = FailingCol = TacticLine = TacticCol = CallingLine = CallingCol = -1;

      if(!ActiveTacticWasNotUsed()) ResolveCorrectLocations();
    }

    private bool ActiveTacticWasNotUsed()
    {
      var x =_tmpFailingMember as Method;
      return (from stmt in x?.Body.Body
              select stmt as UpdateStmt into upstmt
              where upstmt?.Lhss.Count == 0 && upstmt.Rhss.Count > 0
              select upstmt.Rhss[0] as ExprRhs into rhs
              select rhs?.Expr as ApplySuffix into rhsExpr
              select rhsExpr?.Lhs as NameSegment into callName
              select callName?.Name == _activeTactic.Name).FirstOrDefault();
    }

    private static string MethodNameFromImpl(string implName)
    {
      Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));
      var matches = Regex.Match(implName, @".*\$\$.*\..*\.(.*)");
      return matches.Groups[1].Value;
    }
    
    private Bpl.IToken GetFailingLine()
    {
      var offsetToCallingLineInSource = CallingLine - _callingMember.BodyStartTok.line;
      var offsetToInsertedLinesInTmp = _tmpFailingMember.BodyStartTok.line + offsetToCallingLineInSource;
      var offsetToFailingLineInTemp = _errTok.line - offsetToInsertedLinesInTmp;
      var offsetToFailingLineInSource = _activeTactic.BodyStartTok.line + offsetToFailingLineInTemp;

      return (from stmt in _activeTactic.Body.SubStatements.ToArray()
              where stmt.Tok.line == offsetToFailingLineInSource + 1
              select stmt.Tok).FirstOrDefault();
    }

    private void ResolveCorrectLocations()
    { 
      Contract.Ensures(CallingLine!=-1);
      Contract.Ensures(CallingCol != -1);
      Contract.Ensures(TacticLine != -1);
      Contract.Ensures(TacticCol != -1);
      CallingLine = _tacticCall.Tok.line;
      CallingCol = _tacticCall.Tok.col;
      
      TacticLine = _activeTactic.BodyStartTok.line;
      TacticCol = _activeTactic.BodyStartTok.col;

      var failing = GetFailingLine();
      if (failing == null) return;
      FailingCol = failing.col;
      FailingLine = failing.line;
    }

    public void AddTacticErrors(List<DafnyError> existingErrors, ITextSnapshot snap, string file)
    {
      Contract.Requires(snap != null);
      Contract.Requires(existingErrors!=null);
      Contract.Requires(!string.IsNullOrEmpty(file));
      if (!(FoundCalling && FoundTactic)) return;

      var callingError = new DafnyError(file, CallingLine - 1, CallingCol - 1, ErrorCategory.TacticError,
        "Failing Call to " + _activeTactic, snap, true, "");
      var actualError = new DafnyError(file, (FoundFailing ? FailingLine : TacticLine) - 1,
        (FoundFailing ? FailingCol : TacticCol) - 1, ErrorCategory.TacticError,
        _errMessage, snap, true, "");
      
      CheckAndAddDupe(callingError, existingErrors);
      CheckAndAddDupe(actualError, existingErrors);
    }

    private static void CheckAndAddDupe(DafnyError newErr, ICollection<DafnyError> oldErrs) {
      var x = (from e in oldErrs
               where e.Filename == newErr.Filename
               && e.Column == newErr.Column
               && e.Line == newErr.Line
               && e.Message == newErr.Message
               select e).FirstOrDefault();
      if (x != null) return;
      oldErrs.Add(newErr);
    }
  }
}
