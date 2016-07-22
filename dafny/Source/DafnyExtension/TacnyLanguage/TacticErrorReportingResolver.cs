using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using Bpl = Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage.TacnyLanguage
{
  internal class TacticErrorResolutionException : Exception
  {
    public TacticErrorResolutionException(string msg) : base(msg){}
  }
  internal class TacticErrorReportingResolver
  {
    private readonly Bpl.Token _errTok;
    private readonly DefaultClassDecl  _tmpModule;
    private readonly UpdateStmt _tacticCall;
    private readonly Tactic _activeTactic;
    private readonly string _errMessage, _implTargetName;
    public int FailingLine, FailingCol, TacticLine, TacticCol, CallingLine, CallingCol;

    public bool FoundFailing => !(FailingLine == -1 || FailingCol == -1);
    public bool FoundTactic => !(TacticLine == -1 || TacticCol == -1);
    public bool FoundCalling => !(CallingLine == -1 || CallingCol == -1);

    public TacticErrorReportingResolver(Tacny.CompoundErrorInformation errorInfo)
    {
      Contract.Ensures(_errTok != null);
      Contract.Ensures(_errMessage != null);
      Contract.Ensures(_tmpModule != null);
      Contract.Ensures(_tacticCall != null);
      Contract.Ensures(_activeTactic != null);
      Contract.Ensures(!string.IsNullOrEmpty(_implTargetName));
      var proofState = errorInfo.S;
      var tmpProgram = ((Tacny.CompoundErrorInformation)errorInfo.E).P;
      var innerError = ((Tacny.CompoundErrorInformation)errorInfo.E).E;

      _errTok = (Bpl.Token)innerError.Tok;
      _errMessage = innerError.FullMsg;

      _tmpModule = (DefaultClassDecl)tmpProgram.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
      _implTargetName = MethodNameFromImpl(innerError.ImplementationName);
      _tacticCall = proofState.TacticApplication;
      _activeTactic = proofState.GetTactic(_tacticCall) as Tactic;

      FailingLine = FailingCol = TacticLine = TacticCol = CallingLine = CallingCol = -1;
      ResolveCorrectLocations();
    }

    private static string MethodNameFromImpl(string implName)
    {
      Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));
      var matches = Regex.Match(implName, @".*\$\$.*\..*\.(.*)");
      return matches.Groups[1].Value;
    }
    
    private int OffsetFromStartOfAddedLinesToFailingLine()
    {
      Contract.Ensures(Contract.Result<int>() >= 0);
      var tmpFailingMethod = _tmpModule.Members.FirstOrDefault(x => x.CompileName == _implTargetName) as Method;
      if (tmpFailingMethod == null) throw new TacticErrorResolutionException("The failing method must exist in tmp file");
      return _errTok.line - tmpFailingMethod.BodyStartTok.line;
    }

    private Bpl.IToken GetFailingLine()
    {
      var offsetToFailure = TacticLine + OffsetFromStartOfAddedLinesToFailingLine();
      return (from stmt in _activeTactic.Body.SubStatements.ToArray()
              where stmt.Tok.line == offsetToFailure
              select stmt.Tok).FirstOrDefault();
    }
    
    private void ResolveCorrectLocations()
    { 
      CallingLine = _tacticCall.Tok.line;
      CallingCol = _tacticCall.Tok.col;
      
      TacticLine = _activeTactic.BodyStartTok.line;
      TacticCol = _activeTactic.BodyStartTok.col;

      var failing = GetFailingLine();
      if (failing == null) return;
      FailingCol = failing.col;
      FailingLine = failing.line;
    }

    public void AddTacticErrors(ResolverTagger errorListHolder, ITextSnapshot snap, string requestId, string file)
    {
      Contract.Requires(errorListHolder != null);
      Contract.Requires(snap != null);
      Contract.Requires(!string.IsNullOrEmpty(requestId));
      Contract.Requires(!string.IsNullOrEmpty(file));
      Contract.Requires(FoundCalling && FoundTactic);

      errorListHolder.AddError(
        new DafnyError(_errTok.filename, 0, 0, ErrorCategory.AuxInformation, 
        _errMessage + $" at ({_errTok.line},{_errTok.col-1})", null, false, null, false),
        "$$program_tactics$$", requestId);
      
      errorListHolder.AddError(
        new DafnyError(file, CallingLine - 1, CallingCol - 1, ErrorCategory.TacticError,
        "Failing Call to " + _activeTactic, snap, true, ""),
        "$$program_tactics$$", requestId);
      
      errorListHolder.AddError(
        new DafnyError(file, (FoundFailing ? FailingLine : TacticLine) - 1,
        (FoundFailing ? FailingCol : TacticCol) - 1, ErrorCategory.TacticError,
        _errMessage, snap, true, ""),
        "$$program_tactics$$", requestId);
    }
  }
}
