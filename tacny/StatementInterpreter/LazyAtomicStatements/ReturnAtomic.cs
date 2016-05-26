using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny {
  class ReturnAtomic : Atomic, IAtomicLazyStmt {
    public ReturnAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return Returns(st);
    }

    private IEnumerable<Solution> Returns(Statement st) {
      IVariable lv = null;
      List<Expression> call_arguments; // we don't care about this
      List<IVariable> input = new List<IVariable>();

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));

      Method source = DynamicContext.md as Method;
      Contract.Assert(source != null, Util.Error.MkErr(st, 4));

      input.AddRange(source.Outs);
      yield return AddNewLocal(lv, input);

    }
  }
}
