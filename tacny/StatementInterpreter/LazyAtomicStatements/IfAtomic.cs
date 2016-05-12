using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Util;
using System;

namespace LazyTacny {
  class IfAtomic : BlockAtomic, IAtomicLazyStmt {


    public IfAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Assert(ExtractGuard(st) != null, Util.Error.MkErr(st, 2));
      /**
       * 
       * Check if the loop guard can be resolved localy
       */
      if (IsResolvable())
        return ExecuteIf(st as IfStmt);
      else
        return InsertIf(st as IfStmt);
    }

    private IEnumerable<Solution> ExecuteIf(IfStmt ifStmt) {
      Contract.Requires(ifStmt != null);
      bool guard_res = false;
      guard_res = EvaluateGuard();
      // if the guard has been resolved to true resolve if body
      if (guard_res) {
        return ResolveBody(ifStmt.Thn);

      } else if (!guard_res && ifStmt.Els != null) // if else statement exists
        {
        // if it a block statement resolve the body
        if (ifStmt.Els is BlockStmt)
          return ResolveBody(ifStmt.Els as BlockStmt);
        else // otherwise it is 'else if' block, resolve recursively
        {
          return ExecuteIf(ifStmt.Els as IfStmt);
        }
      } else
        return default(IEnumerable<Solution>);
    }

    /// <summary>
    /// Insert the if statement into dafny code
    /// </summary>
    /// <param name="ifStmt"></param>
    /// <param name="solution_list"></param>
    /// <returns></returns>
    private IEnumerable<Solution> InsertIf(IfStmt ifStmt) {

      // resolve the if statement guard
      foreach (var result in ResolveExpression(ifStmt.Guard)) {
        var resultExpression = result is IVariable ? IVariableToExpression(result as IVariable) : result as Expression;
        // resolve 'if' block
        var ifStmtEnum = ResolveBody(ifStmt.Thn);
        IEnumerable<Solution> elseStmtEnum = null;

        if (ifStmt.Els != null) {
          // resovle else block
          if (ifStmt.Els is BlockStmt)
            elseStmtEnum = ResolveBody(ifStmt.Els as BlockStmt);
          else // resolve 'else if' block
            elseStmtEnum = InsertIf(ifStmt.Els as IfStmt);
        }

        foreach (var statement in GenerateIfStmt(ifStmt, resultExpression, ifStmtEnum, elseStmtEnum)) {
          yield return statement;
        }
      }

      yield break;
    }

    private IEnumerable<Solution> GenerateIfStmt(IfStmt original, Expression guard, IEnumerable<Solution> ifStmtEnum, IEnumerable<Solution> elseStmtEnum) {
      foreach (var @if in ifStmtEnum) {
        List<Statement> bodyList = @if.state.GetAllUpdated();
        var ifBody = new BlockStmt(original.Thn.Tok, original.Thn.EndTok, bodyList);
        if (elseStmtEnum != null) {
          foreach (var @else in elseStmtEnum) {
            List<Statement> elseList = @else.state.GetAllUpdated();
            Statement elseBody = null;
            // if original body was a plain else block
            if (original.Els is BlockStmt)
              elseBody = new BlockStmt(original.Els.Tok, original.Thn.EndTok, elseList);
            else // otherwise it was a 'else if' and the solution list should only contain one if stmt
              elseBody = elseList[0];

            yield return AddNewStatement<IfStmt>(original, new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(ifBody), elseBody));
          }
        } else {
          yield return AddNewStatement<IfStmt>(original, new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(ifBody), null));
        }
      }
    }
  }
}
