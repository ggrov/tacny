using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  public class IfAtomic : BlockAtomic, IAtomicLazyStmt {


    public IfAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Assert(ExtractGuard(st) != null, Error.MkErr(st, 2));
      /**
       * 
       * Check if the loop guard can be resolved localy
       */
      return IsResolvable() ? ExecuteIf(st as IfStmt) : InsertIf(st as IfStmt);
    }

    private IEnumerable<Solution> ExecuteIf(IfStmt ifStmt) {
      Contract.Requires(ifStmt != null);
      bool guardRes = EvaluateGuard();
      // if the guard has been resolved to true resolve if body
      if (guardRes) {
        foreach (var item in ResolveBody(ifStmt.Thn)) {
          if (item.State.DynamicContext.isPartialyResolved) {
            yield return item;
          }
        }
      } else if (ifStmt.Els != null) {
        // if else statement exists
        // if it a block statement resolve the body
        var els = ifStmt.Els as BlockStmt;
        if (els != null)
          foreach (var item in ResolveBody(els)) {
            yield return item;
          } else { // otherwise it is 'else if' block, resolve recursively
          foreach (var item in ExecuteIf(ifStmt.Els as IfStmt)) {
            if (item.State.DynamicContext.isPartialyResolved) {
              yield return item;
            }
          }
        }
      } else
        yield return default(Solution);
    }

    /// <summary>
    /// Insert the if statement into dafny code
    /// </summary>
    /// <param name="ifStmt"></param>
    /// <returns></returns>
    private IEnumerable<Solution> InsertIf(IfStmt ifStmt) {

      // resolve the if statement guard
      foreach (var result in ResolveExpression(ifStmt.Guard)) {
        var resultExpression = result is IVariable ? VariableToExpression(result as IVariable) : result as Expression;
        // resolve 'if' block
        var ifStmtEnum = ResolveBody(ifStmt.Thn);
        IEnumerable<Solution> elseStmtEnum = null;

        if (ifStmt.Els != null) {
          // resovle else block
          var stmt = ifStmt.Els as BlockStmt;
          elseStmtEnum = stmt != null ? ResolveBody(stmt) : InsertIf(ifStmt.Els as IfStmt);
        }
        foreach (var statement in GenerateIfStmt(ifStmt, resultExpression, ifStmtEnum, elseStmtEnum)) {
          yield return statement;
        }
      }
    }

    private IEnumerable<Solution> GenerateIfStmt(IfStmt original, Expression guard, IEnumerable<Solution> ifStmtEnum, IEnumerable<Solution> elseStmtEnum) {
      foreach (var @if in ifStmtEnum) {
        var bodyList = @if.State.GetAllUpdated();
        var ifBody = new BlockStmt(original.Thn.Tok, original.Thn.EndTok, bodyList);
        if (elseStmtEnum != null) {
          foreach (var @else in elseStmtEnum) {
            var elseList = @else.State.GetAllUpdated();
            Statement elseBody = null;
            // if original body was a plain else block
            if (original.Els is BlockStmt)
              elseBody = new BlockStmt(original.Els.Tok, original.Thn.EndTok, elseList);
            else // otherwise it was a 'else if' and the solution list should only contain one if stmt
              elseBody = elseList[0];

            yield return AddNewStatement(original, new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(ifBody), elseBody));
          }
        } else {
          yield return AddNewStatement(original, new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(ifBody), null));
        }
      }
    }
  }
}
