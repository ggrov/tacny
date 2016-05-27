using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class GetConstructorAtomic : Atomic, IAtomicLazyStmt {

    public GetConstructorAtomic(Atomic atomic) : base(atomic) {

    }
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      yield return (GetConstructor(st));
    }

    private Solution GetConstructor(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs = null;
      InitArgs(st, out lv, out callArgs);

      var ctor = DynamicContext.activeCtor;
      Contract.Assert(ctor != null, Error.MkErr(st, 22));
        

      return AddNewLocal(lv, ctor);
    }
  }
}
