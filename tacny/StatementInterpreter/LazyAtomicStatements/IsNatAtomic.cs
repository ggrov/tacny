using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Tacny;
namespace LazyTacny {

  public class IsNatAtomic : Atomic, IAtomicLazyStmt {


    public IsNatAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      throw new NotImplementedException();
    }


    private IEnumerable<Solution> IsNat(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      object result = null;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 1, Util.Error.MkErr(st, 0, 1, callArgs.Count));
      foreach (var item in ResolveExpression(callArgs[0])) {
        var ns = item as NameSegment;
        if (ns != null) {
          var type = StaticContext.GetVariableType(ns);
          if (type == null) {
            Contract.Assert(false, Util.Error.MkErr(st, 24));
            result = new LiteralExpr(st.Tok, type is IntType);
          }
        }
      }

      yield return AddNewLocal(lv, result);
    }
  }
}