using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  public class WhileAtomic : BlockAtomic, IAtomicLazyStmt {
    public WhileAtomic(Atomic atomic) : base(atomic) { }



    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Assert(ExtractGuard(st) != null, Error.MkErr(st, 2));
      /**
       * Check if the loop guard can be resolved localy
       */
      return IsResolvable() ? ExecuteLoop(st as WhileStmt) : InsertLoop(st as WhileStmt);
    }

    private IEnumerable<Solution> ExecuteLoop(WhileStmt whileStmt) {
      bool guardRes = EvaluateGuard();
      // if the guard has been resolved to true resolve then body
      if (!guardRes) yield break;
      // reset the PartialyResolved
      DynamicContext.isPartialyResolved = false;
      foreach (var item in ResolveBody(whileStmt.Body)) {
        item.State.DynamicContext.isPartialyResolved = true;
        yield return item;
      }
    }

    private IEnumerable<Solution> InsertLoop(WhileStmt whileStmt) {
      Contract.Requires(whileStmt != null);
      ResolveExpression(ref Guard);
      var guard = Guard.TreeToExpression();

      foreach (var item in ResolveBody(whileStmt.Body)) {
        var result = GenerateWhileStmt(whileStmt, guard, item);
        yield return AddNewStatement(result, result);
      }
    }

    private static WhileStmt GenerateWhileStmt(WhileStmt original, Expression guard, Solution body) {
      var bodyList = body.State.GetAllUpdated();
      var thenBody = new BlockStmt(original.Body.Tok, original.Body.EndTok, bodyList);
      return new WhileStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), original.Invariants, original.Decreases, original.Mod, Util.Copy.CopyBlockStmt(thenBody));
    }
  }
}
