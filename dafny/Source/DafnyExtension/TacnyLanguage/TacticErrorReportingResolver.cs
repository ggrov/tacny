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
    private readonly Bpl.ErrorInformation _errorInfo;
    private readonly DefaultClassDecl  _tmpModule;
    private readonly UpdateStmt _tacticCall;
    private readonly Tactic _activeTactic;
    private readonly string _implTargetName;
    public int FailingLine, FailingCol, TacticLine, TacticCol, CallingLine, CallingCol;

    public bool FoundFailing => !(FailingLine == -1 || FailingCol == -1);
    public bool FoundTactic => !(TacticLine == -1 || TacticCol == -1);
    public bool FoundCalling => !(CallingLine == -1 || CallingCol == -1);

    public TacticErrorReportingResolver(Tacny.CompoundErrorInformation errorInfo)
    {
      Contract.Ensures(_errorInfo != null);
      Contract.Ensures(_tmpModule != null);
      Contract.Ensures(_tacticCall != null);
      Contract.Ensures(_activeTactic != null);
      Contract.Ensures(!string.IsNullOrEmpty(_implTargetName));
      var proofState = errorInfo.S;
      var tmpProgram = ((Tacny.CompoundErrorInformation)errorInfo.E).P;

      _errorInfo = ((Tacny.CompoundErrorInformation)errorInfo.E).E;
      _tmpModule = (DefaultClassDecl)tmpProgram.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
      _implTargetName = MethodNameFromImpl(_errorInfo.ImplementationName);
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
      return _errorInfo.Tok.line - tmpFailingMethod.BodyStartTok.line;
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

      var failing = GetFailingLine(); //NOTE: Currently, the failing line is assuming a macro-style of tactic
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
        new DafnyError(_errorInfo.Tok.filename, 0, 0,
          ErrorCategory.AuxInformation, "Temp file for tactics", null, false, "", false),
        "$$program_tactics$$", requestId);

      if (!FoundCalling) return;
      errorListHolder.AddError(
        new DafnyError(file, CallingLine - 1, CallingCol - 1, ErrorCategory.TacticError,
        "Failing Tactic Call - " + _errorInfo.FullMsg, snap, true, ""),
        "$$program_tactics$$", requestId);

      if (!FoundFailing) {
        errorListHolder.AddError(
          new DafnyError(file, TacticLine - 1, TacticCol - 1, ErrorCategory.TacticError,
            "Failing Tactic - " + _errorInfo.FullMsg, snap, true, ""),
          "$$program_tactics$$", requestId);
      } else {
        errorListHolder.AddError(
          new DafnyError(file, FailingLine - 1, FailingCol - 1, ErrorCategory.TacticError,
            "Failing Tactic - " + _errorInfo.FullMsg, snap, true, ""),
          "$$program_tactics$$", requestId);
      }
    }
  }
}
