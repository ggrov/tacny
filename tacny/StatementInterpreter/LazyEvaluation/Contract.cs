using System;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class TacnyContract {
    private readonly Atomic atomic;

    public TacnyContract(Atomic state) {
      Contract.Requires(state != null);
      // check if codeContracts for Tacny are enabled
      atomic = state;
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
        LiteralExpr result = item as LiteralExpr;
        Contract.Assert(result != null, Error.MkErr(expr, 1, "Boolean Expression"));
        Contract.Assert((bool)result.Value, Error.MkErr(expr, 14));
      }
    }
  }
}
