using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Tacny;
using Util;

namespace LazyTacny {
  class VariablesAtomic : Atomic, IAtomicLazyStmt {
    public VariablesAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return GetVariables(st, solution);
    }

    private Solution GetVariables(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> call_arguments;
      List<IVariable> locals = new List<IVariable>();

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));

      Method source = DynamicContext.md as Method;
      Contract.Assert(source != null, Error.MkErr(st, 4));

      locals.AddRange(StaticContext.staticVariables.Values.ToList());

      AddLocal(lv, locals);
      return new Solution(Copy());
    }


  }
}
