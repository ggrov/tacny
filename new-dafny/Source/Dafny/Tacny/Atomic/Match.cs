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

      //state.GetVariableType (datatype);
      Console.WriteLine("line");

      //generate a test program to check which cases need to apply tacny
      var p = new Printer(Console.Out);
      bool[] ctorFlags;
      int ctor; // current active match case
      InitCtorFlags(datatype, out ctorFlags);
      
      MatchStmt ms = GenerateMatchStmt(state.TacticApplication.Tok.line, srcVar.Copy(), datatype);
      //for each brnache, generate code but assume false state for other branches

      //verify t
      p.PrintStatement(ms, 0);
      state.AddStatement(ms);

      var bodyList = new Dictionary<ProofState, BlockStmt>();
        bodyList.Add(state, Util.InsertCode(state,
          new Dictionary<UpdateStmt, List<Statement>>() {
              {state.TacticApplication, state.GetGeneratedCode()}
          }));
      
      var memberList = Util.GenerateMembers(state, bodyList);
      var prog = Util.GenerateDafnyProgram(state, memberList.Values.ToList());
      p.PrintProgram(prog, false);
      var result = Util.ResolveAndVerify(prog, null);

      throw new NotImplementedException();
    }

    private static void InitCtorFlags(DatatypeDecl datatype, out bool[] flags, bool value = false) {
      flags = new bool[datatype.Ctors.Count];
      for(int i = 0; i < flags.Length; i++) {
        flags[i] = value;
      }
    }

    private MatchStmt GenerateMatchStmt (int line, NameSegment ns, DatatypeDecl datatype) {
      Contract.Requires(ns != null);
      Contract.Requires(datatype != null);
      Contract.Ensures(Contract.Result<MatchStmt>() != null);
      List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
      int index = line + 1;
      int i = 0;
      
      foreach(DatatypeCtor dc in datatype.Ctors) {
        MatchCaseStmt mcs = GenerateMatchCaseStmt(index, dc);

        cases.Add(mcs);
        line += mcs.Body.Count + 1;
        i++;
      }

      return new MatchStmt(new Token(index, 0) { val = "match" },
        new Token(index, 0) { val = "=>"}, 
        ns, cases, false);
    }


    private MatchCaseStmt GenerateMatchCaseStmt(int line, DatatypeCtor dtc) {
      Contract.Requires(dtc != null);
      List<CasePattern> casePatterns = new List<CasePattern>();
      MatchCaseStmt mcs;
      dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);

      foreach(var formal in dtc.Formals) {
        CasePattern cp;
        cp = GenerateCasePattern(line, formal);
        casePatterns.Add(cp);
      }

      List<Statement> body = new List<Statement>();
      body.Add(GenerateAssumeFalseStmt(line));
         mcs = new MatchCaseStmt(new Token(line, 0) { val = "cases" },
        dtc.CompileName, casePatterns, body);
      return mcs;
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