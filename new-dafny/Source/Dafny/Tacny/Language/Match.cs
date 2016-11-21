using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Tacny;
using Formal = Microsoft.Dafny.Formal;
using Type = Microsoft.Dafny.Type;
using Microsoft.Dafny;


namespace Tacny.Language {
  class Match {
    public string Signature => "tmatch";
    public bool IsPartial = false;
    private Dictionary<string, Type> _ctorTypes;

    /*
    private List<string> _names;
    private string PopCaseName(){

      if (_names.Count > 0){
        var ret = _names[0];
        _names.Remove(ret);
        return ret;
      }
      else
        return null;
    }

    public static List<string> ParseDefaultCasesNames(Statement stmt) {

      List<string> n = new List<string>();
      if (stmt.Attributes != null) { 
      if (stmt.Attributes.Name == "vars"){
        foreach (Expression x in stmt.Attributes.Args){
          if (x is NameSegment){
            var y = x as NameSegment;
            n.Add(y.Name);
          }
        }
      }
    }
      return n;
    }
    
    public Match(){
      _names = new List<string>();
    }
    
    public Match(Statement stmt) {
      _names = ParseDefaultCasesNames(stmt);
    }
    */

    public Match(){
    }


    public static bool IsTerminated(List<List<Statement>> raw, bool b){
      Contract.Requires(raw != null);
      Contract.Requires(raw.Count > 0);
      Contract.Requires(raw[0]!=null && raw[0].Count == 1 && raw[0][0] is MatchStmt);

      //raw[0] is the match case stmt with assume false for each case
      //raw[1..] the actual code to bt inserted in the case statement

       return (raw[0][0] as MatchStmt).Cases.Count + 1== raw.Count;
    }

    public static List<Statement> Assemble(List<List<Statement>> raw){
      //Contract.Requires(IsTerminated(raw));

      var matchStmt = raw[0][0] as MatchStmt;

      for (int i = 1; i < raw.Count; i++){
        matchStmt.Cases[i-1].Body.Clear();
        matchStmt.Cases[i - 1].Body.AddRange(raw[i]);
      }
      var ret =  new List<Statement>();
      ret.Add(matchStmt);
      return ret;
    }

    internal int GetNthCaseIdx(List<List<Statement>> raw) {
      Contract.Requires(raw != null);
      Contract.Requires(raw.Count > 0);
      Contract.Requires(raw[0] != null && raw[0].Count == 1 && raw[0][0] is MatchStmt);
      return raw.Count - 1;

    }
    public IEnumerable<ProofState> EvalNext(Statement statement, ProofState state0){
      Contract.Requires(statement != null);
      Contract.Requires(statement is TacnyCasesBlockStmt);
      var state = state0.Copy();

      var stmt = statement as TacnyCasesBlockStmt;
      var raw = state.GetGeneratedaRawCode();


      state.AddNewFrame(stmt.Body.Body, IsPartial);

      var matchStmt = raw[0][0] as MatchStmt;

      var idx = GetNthCaseIdx(raw);
      foreach(var tmp in matchStmt.Cases[idx].CasePatterns) {
        state.AddDafnyVar(tmp.Var.Name, new ProofState.VariableData { Variable = tmp.Var, Type = tmp.Var.Type });
      }
      //with this flag set to true, dafny will check the case branch before evaluates any tacny code
      state.IfVerify = true;
      yield return state;
     
    }

    public IEnumerable<ProofState> EvalInit(Statement statement, ProofState state0){
      Contract.Requires(statement != null);
      Contract.Requires(statement is TacnyCasesBlockStmt);
      var state = state0.Copy();

      var stmt = statement as TacnyCasesBlockStmt;
      var p = new Printer(Console.Out);
      NameSegment caseVar;

      //get guards
      Debug.Assert(stmt != null, "stmt != null");
      var guard = stmt.Guard as ParensExpression;

      if(guard == null)
        caseVar = stmt.Guard as NameSegment;
      else
        caseVar = guard.E as NameSegment;

      //TODO: need to check the datatype pf caseGuard, 
      // also need to consider the case that caseVar is a tac var
      var srcVar = state.GetTacnyVarValue(caseVar) as NameSegment;
      var srcVarData = state.GetDafnyVar(srcVar.Name);
      var datatype = state.GetDafnyVarType(srcVar.Name).AsDatatype;


      //generate a test program to check which cases need to apply tacny
      bool[] ctorFlags;
      int ctor; // current active match case
      InitCtorFlags(datatype, out ctorFlags);

      List<Func<int, List<Statement>>> fList = new List<Func<int, List<Statement>>>();

      int i;
      for(i = 0; i < datatype.Ctors.Count; i++) {
        fList.Add(GenerateAssumeFalseStmtAsStmtList);
      }

      //var matchStmt = GenerateMatchStmt(state.TacticApplication.Tok.line, srcVar.Copy(), datatype, fList);
      var matchStmt = GenerateMatchStmt(Interpreter.TACNY_CODE_TOK_LINE, srcVar.Copy(), datatype, fList);

      //use a dummystmt to creat a frame for match, note that this stmts is never be evaluated
      var dummystmt = new List<Statement>();
      for(i = 0; i < datatype.Ctors.Count; i++) {
        dummystmt.Add(stmt);
      }

      state.AddNewFrame(dummystmt, IsPartial, Signature);
      //add raw[0]
      state.AddStatement(matchStmt);

      //push a frame for the first case
      //TODO: add case variable to frame, so that variable () can refer to it
      state.AddNewFrame(stmt.Body.Body, IsPartial);

      foreach(var tmp in matchStmt.Cases[0].CasePatterns) {
        state.AddDafnyVar(tmp.Var.Name, new ProofState.VariableData { Variable = tmp.Var, Type = tmp.Var.Type });
      }
      //with this flag set to true, dafny will check the case brnach before evaluates any tacny code
      state.IfVerify = true;
      yield return state;
    }

    private IEnumerable<ProofState> StmtHandler(List<Statement> stmts, ProofState state){
      IEnumerable<ProofState> enumerable = null;
      ProofState ret = state;

      foreach (var stmt in stmts){
        if (stmt is TacticVarDeclStmt){
          enumerable = Interpreter.RegisterVariable(stmt as TacticVarDeclStmt, ret);
          var e = enumerable.GetEnumerator();
          e.MoveNext();
          ret = e.Current;
        }
        else if (stmt is PredicateStmt){
          enumerable = Interpreter.EvalPredicateStmt((PredicateStmt) stmt, ret);
          var e = enumerable.GetEnumerator();
          e.MoveNext();
          ret = e.Current;
        }
      }

      foreach(var item in enumerable)
        yield return item.Copy();
    }


    private static void InitCtorFlags(DatatypeDecl datatype, out bool[] flags, bool value = false) {
      flags = new bool[datatype.Ctors.Count];
      for(int i = 0; i < flags.Length; i++) {
        flags[i] = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="line"></param>
    /// <param name="ns"></param>
    /// <param name="datatype"></param>
    /// <param name="f"></param>a function list which contains a function to generate stetment list with given line number
    /// <returns></returns>
    private MatchStmt GenerateMatchStmt (int line, NameSegment ns, DatatypeDecl datatype, List<Func<int, List<Statement>>> fL) {
      Contract.Requires(ns != null);
      Contract.Requires(datatype != null);
      Contract.Ensures(Contract.Result<MatchStmt>() != null);
      List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
      int index = Interpreter.TACNY_CODE_TOK_LINE;//line + 1;
      int i = 0;


      for (int j = 0; j < datatype.Ctors.Count; j++){
        var dc = datatype.Ctors[j];
        Func<int, List<Statement>> f = _=> new List<Statement>();
        if (j < fL.Count) f = fL[j];

        MatchCaseStmt mcs = GenerateMatchCaseStmt(index, dc, f);

        cases.Add(mcs);
        line += mcs.Body.Count + 1;
        i++;
      }

      return new MatchStmt(new Token(index, 0) { val = "match" },
        new Token(index, 0) { val = "=>"}, 
        ns, cases, false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="line"></param>
    /// <param name="dtc"></param>
    /// <param name="f"></param>
    /// <returns></returns>
    private MatchCaseStmt GenerateMatchCaseStmt(int line, DatatypeCtor dtc,  Func<int,List<Statement>> f) {
      Contract.Requires(dtc != null);
      List<CasePattern> casePatterns = new List<CasePattern>();
      MatchCaseStmt mcs;
      dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);

      foreach(var formal in dtc.Formals) {
        CasePattern cp;
        cp = GenerateCasePattern(line, formal);
        casePatterns.Add(cp);
      }

      //List<Statement> body = new List<Statement>();
      //body.Add(GenerateAssumeFalseStmt(line));
         mcs = new MatchCaseStmt(new Token(line, 0) { val = "cases" },
        dtc.CompileName, casePatterns, f(line));
      return mcs;
    }

    private List<Statement> GenerateAssumeFalseStmtAsStmtList(int line){
      var l =  new List<Statement>();
      l.Add(GenerateAssumeFalseStmt(line));
      return l;
    } 

    private AssumeStmt GenerateAssumeFalseStmt(int line){
      return new AssumeStmt(new Token(line, 0){val = "assume"},
        new Token(line, 0){val = ";"},
        new Microsoft.Dafny.LiteralExpr(new Token(line, 0) { val = "false" }, false), 
        null);
      
    }

    private CasePattern GenerateCasePattern(int line, Formal formal) {
      Contract.Requires(formal != null);
/*      var name = PopCaseName();
      if (name == null) name = formal.Name; 
 */
      formal = new Formal(formal.tok, formal.Name, formal.Type, formal.InParam, formal.IsGhost);
      CasePattern cp = new CasePattern(new Token(line, 0) { val = formal.Name },
        new BoundVar(new Token(line, 0) { val = formal.Name }, formal.Name, new InferredTypeProxy()));
      return cp;
    }

  }
}