using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny {
  class FunctionsAtomic : Atomic, IAtomicLazyStmt {
    public FunctionsAtomic(Atomic atomic) : base(atomic) { }



    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return Functions(st);
    }

    private Solution Functions(Statement st) {
      IVariable lv = null;
      List<Expression> callArguments; // we don't care about this
      var functions = new List<MemberDecl>();

      InitArgs(st, out lv, out callArguments);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(callArguments, 0), Util.Error.MkErr(st, 0, 0, callArguments.Count));

      foreach (var member in StaticContext.program.Members.Values) { 
        Function fun = member as Function;
        if (fun != null)
          functions.Add(fun);
      }
      return AddNewLocal(lv, functions);
      
    }


  }
}
