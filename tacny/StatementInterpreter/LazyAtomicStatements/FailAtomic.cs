using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace LazyTacny {
  class FailAtomic : Atomic, IAtomicLazyStmt {
    public FailAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Assert(st is TacticVarDeclStmt);
      List<Expression> args = null;
      IVariable lv = null;
      InitArgs(st, out lv, out args);
      Dafny.LiteralExpr lit = new Dafny.LiteralExpr(st.Tok, false);
      var ac = this.Copy();
      ac.DynamicContext.AddLocal(lv, lit);
      yield return new Solution(ac);
      yield break;
    }
  }
}
