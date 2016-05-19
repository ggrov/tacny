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
      throw new NotImplementedException();
    }


    private IEnumerable<Solution> Constants(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      var result = new List<Expression>();
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 1, Util.Error.MkErr(st, 0, 1, callArgs.Count));

      foreach(var arg1 in ResolveExpression(callArgs[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Util.Error.MkErr(st, 1, "Expression"));
        var expt = ExpressionTree.ExpressionToTree(expression);
        var leafs = expt.GetLeafData();
        foreach(var leaf in leafs) {
          if(leaf is LiteralExpr) {
            if(!result.Exists(j => (j as LiteralExpr)?.Value == (leaf as LiteralExpr)?.Value)) {
              result.Add(leaf);
            }
          } else if(leaf is ExprDotName) {
            
            var edn = leaf as ExprDotName;
            var ns = edn.Lhs as NameSegment;
            if (result.Exists(j => ExprDotEquality(j, edn))) {
              result.Add(leaf);
            }
          }
        }

      }

      yield return AddNewLocal(lv, result);
    }


    private static bool ExprDotEquality(Expression a, ExprDotName b) {
      var dotName = a as ExprDotName;
      if (dotName == null)
        return false;
      var nsA = dotName.Lhs as NameSegment;
      var nsB = b.Lhs as NameSegment;
      return nsA.Name == nsB.Name && dotName.AsStringLiteral() == b.AsStringLiteral();
    }
  }
}