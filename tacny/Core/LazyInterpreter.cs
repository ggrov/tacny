using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System.Diagnostics;


namespace LazyTacny {
  public class Interpreter {
    private Tacny.Program tacnyProgram;
    private SolutionList solution_list = new SolutionList();

    public Interpreter(Tacny.Program tacnyProgram) {
      Contract.Requires(tacnyProgram != null);
      this.tacnyProgram = tacnyProgram;
      this.solution_list = new SolutionList();
      //Console.SetOut(System.IO.TextWriter.Null);
    }

    public Program ResolveProgram() {

      if (!tacnyProgram.HasTacticApplications()) {
        return tacnyProgram.ParseProgram();
      }
      foreach (var @class in tacnyProgram.topLevelClasses) {
        tacnyProgram.currentTopLevelClass = @class;
        if (tacnyProgram.currentTopLevelClass.tactics.Count < 1)
          continue;
        foreach (var member in tacnyProgram.members) {
          var res = LazyScanMemberBody(member.Value);
          if (res != null) {
            solution_list.Add(res);
            solution_list.Fin();

          }
        }
      }
      // temp hack
      List<Solution> final = new List<Solution>();
      foreach (var solution in solution_list.GetFinal())
        final.Add(solution[0]);

      Dafny.Program prog = tacnyProgram.ParseProgram();
      foreach (var solution in final) {
        solution.GenerateProgram(ref prog);
      }
      tacnyProgram.dafnyProgram = prog;

      return prog;

    }

    private Solution LazyScanMemberBody(MemberDecl md) {
      Contract.Requires(md != null);

      Debug.WriteLine(String.Format("Scanning member {0} body", md.Name));
      if (md is Function) {
        var fun = md as Function;
        Tacny.ExpressionTree expt = null;
        if (fun.Body == null)
          return null;
        expt = Tacny.ExpressionTree.ExpressionToTree(fun.Body);
        expt.FindAndResolveTacticApplication(tacnyProgram, fun);
        /* No reason ot generate new solution
         * if nothing has been changed 
         */
        if (!expt.modified)
          return null;
        var res = expt.TreeToExpression();
        var newFun = new Function(fun.tok, fun.Name, fun.HasStaticKeyword, fun.IsProtected, fun.IsGhost, fun.TypeArgs, fun.Formals, fun.ResultType, fun.Req, fun.Reads, fun.Ens, fun.Decreases, res, fun.Attributes, fun.SignatureEllipsis);
        var ac = new Atomic();
        ac.IsFunction = true;
        ac.DynamicContext.newTarget = newFun;
        return new Solution(ac);

      } else if (md is Method) {
        Method m = md as Method;
        if (m.Body == null)
          return null;

        List<IVariable> variables = new List<IVariable>();
        List<IVariable> resolved = tacnyProgram.GetResolvedVariables(md);
        variables.AddRange(m.Ins);
        variables.AddRange(m.Outs);
        resolved.AddRange(m.Ins); // add input arguments as resolved variables

        // does not work for multiple tactic applications
        foreach (var st in m.Body.Body) {
          UpdateStmt us = null;
          WhileStmt ws = null;
          // register local variables
          if (st is VarDeclStmt) {
            VarDeclStmt vds = st as VarDeclStmt;
            variables.AddRange(vds.Locals);
          } else if (st is UpdateStmt) {
            us = st as UpdateStmt;
          } else if (st is WhileStmt) {
            ws = st as WhileStmt;
            us = ws.TacAps as UpdateStmt;
            foreach (var wst in ws.Body.Body) {
              if (st is VarDeclStmt) {
                VarDeclStmt vds = st as VarDeclStmt;
                variables.AddRange(vds.Locals);
              }
            }
          }
          if (us != null && this.tacnyProgram.IsTacticCall(us)) {
            Debug.WriteLine("Tactic call found");
            return ResolveTactic(variables, resolved, us, md, ws);
          }
        }
      }
      return null;
    }

    private Solution ResolveTactic(List<IVariable> variables, List<IVariable> resolved, UpdateStmt us, MemberDecl md, WhileStmt ws = null) {
      try {

        Solution result = Atomic.ResolveTactic(us, md, tacnyProgram, variables, resolved, ws);
        Solution.PrintSolution(result);
        //Solution.PrintSolution(result);
        this.tacnyProgram.currentDebug.Fin();
        return result;
      } catch (Exception e) {
        Util.Printer.Error(e.Message);
        return null;
      }
    }
  }
}
