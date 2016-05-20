using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Tacny;
namespace LazyTacny {

  public class AssertAtomic : Atomic, IAtomicLazyStmt {


    public AssertAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      throw new NotImplementedException();
    }


    private IEnumerable<Solution> Assert(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      object result = null;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 0, Util.Error.MkErr(st, 0, 0, callArgs.Count));


      yield return AddNewLocal(lv, result);
    }
  }
}