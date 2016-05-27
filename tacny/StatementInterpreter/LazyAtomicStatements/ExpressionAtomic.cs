using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace LazyTacny {
  public class ExpressionAtomic : Atomic, IAtomicLazyStmt {

    public ExpressionAtomic(Atomic atomic) : base(atomic) { }
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return ResolveBexp(st, solution);
    }


    private IEnumerable<Solution> ResolveBexp(Statement st, Solution solution) {
      // unwarp binary expr
      var updateStmt = st as UpdateStmt;
      Contract.Assert(updateStmt != null, "updateStmt == null");
      var aps = ((ExprRhs)updateStmt?.Rhss[0])?.Expr as ApplySuffix;
      var bexp = aps?.Lhs as TacnyBinaryExpr;
      if (bexp == null)
        Contract.Assert(false);

      Contract.Assert(bexp != null, "updateStmt == null");
      switch (bexp.Op) {
        case TacnyBinaryExpr.TacnyOpcode.TacnyOr:
          foreach (var result in ResolveExpression(bexp.E0)) {
            var ac = Copy();
            if (result is Expression)
              ac.DynamicContext.generatedExpressions.Add(result as Expression);
            else if (result is IVariable)
              ac.DynamicContext.generatedExpressions.Add(VariableToExpression(result as IVariable));
            else
              Contract.Assert(false, "Sum tin wong");
            yield return new Solution(ac);
          }
          foreach (var result in ResolveExpression(bexp.E1)) {
            var ac = Copy();
            if (result is Expression)
              ac.DynamicContext.generatedExpressions.Add(result as Expression);
            else if (result is IVariable)
              ac.DynamicContext.generatedExpressions.Add(VariableToExpression(result as IVariable));
            else
              Contract.Assert(false, "Sum tin wong");
            yield return new Solution(ac);
          }
          yield break;
        default:
          Contract.Assert(false); // unknown operator
          yield break;
      }
    }
  }
}
