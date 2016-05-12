using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;

namespace LazyTacny {
  class TacnyContract {
    private readonly Atomic atomic;

    public TacnyContract(Atomic state) {
      Contract.Requires(state != null);
      // check if codeContracts for Tacny are enabled
      this.atomic = state;
    }

    public static void ValidateRequires(Atomic state) {
      Contract.Requires(state != null);
      TacnyContract tc = new TacnyContract(state);
      tc.ValidateRequires();
    }


    protected void ValidateRequires() {
      if (atomic == null)
        return;
      // for  now support only tactics
      var tactic = atomic.DynamicContext.tactic as Tactic;
      foreach (var req in tactic.Req)
        ValidateOne(req);
    }

    protected void ValidateOne(MaybeFreeExpression mfe) {
      Contract.Requires<ArgumentNullException>(mfe != null);
      Expression expr = mfe.E;

      foreach (var item in atomic.ResolveExpression(expr)) {
        Dafny.LiteralExpr result = item as Dafny.LiteralExpr;
        Contract.Assert(result != null, Util.Error.MkErr(expr, 1, "Boolean Expression"));
        Contract.Assert((bool)result.Value, Util.Error.MkErr(expr, 14));
      }
    }
  }
}
