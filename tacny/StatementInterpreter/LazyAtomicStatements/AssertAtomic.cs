using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {

  public class AssertAtomic : Atomic, IAtomicLazyStmt {


    public AssertAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Assert(st);
      yield return new Solution(solution.State.Copy());
    }


    private void Assert(Statement st) {
      IVariable lv;
      List<Expression> callArgs;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 0, Error.MkErr(st, 0, 0, callArgs.Count));

      var tassert = st as TacticAssertStmt;
      var expr = tassert?.Expr;
      foreach (var item in ResolveExpression(expr)) {
        if(!(item is LiteralExpr)) {
          Contract.Assert(false, Error.MkErr(st, 25));
        } else {
          var lit = item as LiteralExpr;
          if(lit.Value is bool) {
            Contract.Assert(((bool)lit.Value), Error.MkErr(st, 26));
          }
        }
        
      }
    }
  }
}