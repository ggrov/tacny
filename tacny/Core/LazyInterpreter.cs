using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Tacny;
using Printer = Util.Printer;
using Program = Tacny.Program;

namespace LazyTacny {
  public class Interpreter {
    private readonly Program _tacnyProgram;
    private readonly SolutionList _solutionList;

    public Interpreter(Program tacnyProgram) {
      Contract.Requires(tacnyProgram != null);
      _tacnyProgram = tacnyProgram;
      _solutionList = new SolutionList();
    }

    public Microsoft.Dafny.Program ResolveProgram() {

    //  _tacnyProgram.PrintBoogieProgram();
      if (!_tacnyProgram.HasTacticApplications()) {
        return _tacnyProgram.ParseProgram();
      }
      foreach (var @class in _tacnyProgram.TopLevelClasses) {
        _tacnyProgram.CurrentTopLevelClass = @class;
        if (_tacnyProgram.CurrentTopLevelClass.Tactics.Count < 1)
          continue;
        Parallel.ForEach(_tacnyProgram.Members, (member) => {

          var res = LazyScanMemberBody(member.Value);
          if (res == null) return;
          lock (_solutionList) {
            _solutionList.Add(res);
            _solutionList.Fin();
          }
        });
        //foreach (var member in _tacnyProgram.Members) {
        //  var res = LazyScanMemberBody(member.Value);
        //  if (res == null) continue;
        //  _solutionList.Add(res);
        //  _solutionList.Fin();
        //}
      }
      // temp hack
      List<Solution> final = new List<Solution>();
      lock (_solutionList)
      {
        foreach (var solution in _solutionList.GetFinal())
          final.Add(solution[0]);
      }

      Microsoft.Dafny.Program prog = _tacnyProgram.ParseProgram();
      foreach (var solution in final) {
        solution.GenerateProgram(ref prog);
      }
      _tacnyProgram.DafnyProgram = prog;

      return prog;

    }

    private Solution LazyScanMemberBody(MemberDecl md) {
      Contract.Requires(md != null);
      Console.WriteLine($"Starting thread: {System.Threading.Thread.CurrentThread.Name}");
      Debug.WriteLine($"Scanning member {md.Name} body");
      var function = md as Function;
      if (function != null) {
        var fun = function;
        if (fun.Body == null)
          return null;
        var expt = ExpressionTree.ExpressionToTree(fun.Body);
        expt.FindAndResolveTacticApplication(_tacnyProgram, fun);
        /* No reason ot generate new solution
         * if nothing has been changed 
         */
        if (!expt.Modified)
          return null;
        var res = expt.TreeToExpression();
        var newFun = new Function(fun.tok, fun.Name, fun.HasStaticKeyword, fun.IsProtected, fun.IsGhost, fun.TypeArgs, fun.Formals, fun.ResultType, fun.Req, fun.Reads, fun.Ens, fun.Decreases, res, fun.Attributes, fun.SignatureEllipsis);
        var ac = new Atomic {
          IsFunction = true,
          DynamicContext = { newTarget = newFun }
        };
        return new Solution(ac);

      }
      var m = md as Method;
      if (m?.Body == null)
        return null;

      List<IVariable> variables = new List<IVariable>();
      List<IVariable> resolved;
      lock (_tacnyProgram) {
       resolved = _tacnyProgram.GetResolvedVariables(md);
      }
      variables.AddRange(m.Ins);
      variables.AddRange(m.Outs);
      resolved.AddRange(m.Ins); // add input arguments as resolved variables
      resolved.AddRange(m.Outs);
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
            if (!(wst is VarDeclStmt)) continue;
            var vds = wst as VarDeclStmt;
            variables.AddRange(vds.Locals);
          }
        }
        if (us == null || !_tacnyProgram.IsTacticCall(us)) continue;
        Debug.WriteLine("Tactic call found");
        return ResolveTactic(variables, resolved, us, md, ws);
      }
      return null;
    }

    private Solution ResolveTactic(List<IVariable> variables, List<IVariable> resolved, UpdateStmt us, MemberDecl md, WhileStmt ws = null) {
      try
      {
        var prog = Util.ObjectExtensions.Copy(_tacnyProgram);
        var result = Atomic.ResolveTactic(us, md, prog, variables, resolved, ws);
        prog.PrintDebugData(prog.CurrentDebug);
        return result;
      } catch (Exception e) {
        Printer.Error(e.Message);
        return null;
      }
    }
  }
}
