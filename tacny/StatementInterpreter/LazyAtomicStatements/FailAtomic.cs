using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace LazyTacny {
  class FailAtomic : Atomic, IAtomicLazyStmt {
    public FailAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Assert(st is TacticVarDeclStmt);
      List<Expression> args = null;
      IVariable lv = null;
      InitArgs(st, out lv, out args);
      LiteralExpr lit = new LiteralExpr(st.Tok, false);
      var ac = Copy();
      ac.DynamicContext.AddLocal(lv, lit);
      yield return new Solution(ac);
    }
  }
}
