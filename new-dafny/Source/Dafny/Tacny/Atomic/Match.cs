using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Formal = Microsoft.Dafny.Formal;
using Type = Microsoft.Dafny.Type;

namespace Tacny.Atomic {
  class Match : Atomic {
    public override string Signature => "tmatch";
    public override int ArgsCount => -1;

    private List<string> _names;
  
    private Dictionary<string, Type> _ctorTypes;

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
      if(stmt.Attributes.Name == "vars") {
        foreach(Expression x in stmt.Attributes.Args) {
          if(x is NameSegment) {
            var y = x as NameSegment;
            n.Add(y.Name);
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

    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {   
      var stmt = statement as TacnyCasesBlockStmt;

      NameSegment caseVar;

      //get guards
      var guard = stmt.Guard as ParensExpression;
      if(guard == null)
        caseVar = stmt.Guard as NameSegment;
      else
        caseVar = guard.E as NameSegment;

      //TODO: need to check the datatype pf caseGuard, 
      // also need to consider the case that caseVar is a tac var
      var srcVar = state.GetLocalValue(caseVar) as NameSegment;
      var srcVarData = state.GetVariable(srcVar.Name);
      var datatype = state.GetVariableType(srcVar.Name).AsDatatype;


      //generate block stmts for each cases

      //generate a blockstmt c
     // state.AddNewFrame();
      //state.GetVariableType (datatype);
      //Console.WriteLine("line");

      //generate a test program to check which cases need to apply tacny
      var p = new Printer(Console.Out);
      bool[] ctorFlags;
      int ctor; // current active match case
      InitCtorFlags(datatype, out ctorFlags);

      List<Func<int, List<Statement>>> fList = new List<Func<int, List<Statement>>>();

      int i;
      for(i = 0; i < datatype.Ctors.Count; i++) {
        fList.Add(GenerateAssumeFalseStmtAsStmtList);
      }
      //find the first case which fails to verify
      for(i = 0; i < datatype.Ctors.Count; i++){
        var state0 = state.Copy();
        fList[i] = _ => new List<Statement>();
        MatchStmt ms = GenerateMatchStmt(state0.TacticApplication.Tok.line, srcVar.Copy(), datatype, fList);
        state0.AddStatement(ms);
        var bodyList = new Dictionary<ProofState, BlockStmt>();
        bodyList.Add(state0, Util.InsertCode(state0,
          new Dictionary<UpdateStmt, List<Statement>>() {
              {state0.TacticApplication, state0.GetGeneratedCode()}
          }));

        var memberList = Util.GenerateMembers(state0, bodyList);
        var prog = Util.GenerateDafnyProgram(state0, memberList.Values.ToList());
        p.PrintProgram(prog, false);
        var result = Util.ResolveAndVerify(prog, null);

        if (result.Count != 0)
          break;
      }

      
     //MatchStmt ms = GenerateMatchStmt(state.TacticApplication.Tok.line, srcVar.Copy(), datatype, fList);
     //for each branch, generate code but assume false state for other branches

     //verify t
     // p.PrintStatement(ms, 0);

     foreach(var stmt0 in stmt.Body.SubStatements) {
        if(stmt0 is TacticVarDeclStmt) {
          var enumerable = Interpreter.RegisterVariable(stmt0 as TacticVarDeclStmt, state);
          var e = enumerable.GetEnumerator();
          e.MoveNext();
          state = e.Current;
        } else if(stmt0 is PredicateStmt){
          fList[i] = GenerateAssumeFalseStmtAsStmtList;
        }
      }
      var finalMs = GenerateMatchStmt(state.TacticApplication.Tok.line, srcVar.Copy(), datatype, fList);
      state.AddStatement(finalMs);
      state.IfVerify = true;

      yield return state;


      // throw new NotImplementedException();
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
          enumerable = Interpreter.ResolvePredicateStmt((PredicateStmt) stmt, ret);
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
      int index = line + 1;
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