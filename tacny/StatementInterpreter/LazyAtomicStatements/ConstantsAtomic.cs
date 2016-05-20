using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Tacny;
using System.Linq;

namespace LazyTacny {

  public class ConstantsAtomic : Atomic, IAtomicLazyStmt {


    public ConstantsAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return Constants(st);
    }


    private Solution Constants(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      var result = new List<Expression>();
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 1, Util.Error.MkErr(st, 0, 1, callArgs.Count));

      foreach(var arg1 in ResolveExpression(callArgs[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Util.Error.MkErr(st, 1, "Term"));
        var expt = ExpressionTree.ExpressionToTree(expression);
        var leafs = expt.GetLeafData();
        foreach(var leaf in leafs) {
          if(leaf is LiteralExpr) {
            if(!result.Exists(j => (j as LiteralExpr)?.Value == (leaf as LiteralExpr)?.Value)) {
              result.Add(leaf);
            }
          } else if(leaf is ExprDotName) {
            var edn = leaf as ExprDotName;
            if (!result.Exists(j => SingletonEquality(j, edn))) {
              result.Add(leaf);
            }
          }
        }

      }

      return AddNewLocal(lv, result);
    }


   
  }
}