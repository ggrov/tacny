using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class GuardAtomic : Atomic, IAtomicLazyStmt {
    public GuardAtomic(Atomic atomic) : base(atomic) { }


    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      WhileStmt ws = null;
      IVariable lv = null;
      List<Expression> call_arguments; // we don't care about this
      List<Expression> guards = new List<Expression>();
      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));

      if(DynamicContext.whileStmt != null) {
        guards.Add(DynamicContext.whileStmt.Guard);
      } else {
        var md = DynamicContext.md as Method;
        //Contract.Assert(md != null, Util.Error.MkErr(st, 23, "method"));
        foreach (var wst in md?.Body.Body) {
          if (wst is WhileStmt)
            guards.Add((wst as WhileStmt).Guard);
        }
      }
      
      yield return AddNewLocal(lv, guards);
    }
  }
}
