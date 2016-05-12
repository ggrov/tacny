using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

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
      Dafny.LiteralExpr lit = new Dafny.LiteralExpr(st.Tok, true);
      var ac = this.Copy();
      ac.DynamicContext.AddLocal(lv, lit);
      yield return new Solution(ac);
      yield break;
    }
  }
}
