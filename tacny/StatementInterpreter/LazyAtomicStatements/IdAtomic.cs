using System.Collections.Generic;
using Microsoft.Dafny;

namespace LazyTacny {
  class IdAtomic : Atomic, IAtomicLazyStmt {
    public IdAtomic(Atomic atomic) : base(atomic) { }

    /// <summary>
    /// Identity expression
    /// </summary>
    /// <param name="st"></param>
    /// <param name="solution"></param>
    /// <returns></returns>
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      List<Expression> args = null;
      IVariable lv = null;
      InitArgs(st, out lv, out args);
      LiteralExpr lit = new LiteralExpr(st.Tok, true);
      var ac = Copy();
      ac.DynamicContext.AddLocal(lv, lit);
      yield return new Solution(ac);
    }
  }
}
