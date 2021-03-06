﻿using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Tacny;
using Util;

namespace LazyTacny {
  class DeleteAtomic : Atomic, IAtomicLazyStmt {
    public DeleteAtomic(Atomic atomic) : base(atomic) { }
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> callArgs = null;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(tcce.OfSize(callArgs, 2), Error.MkErr(st, 0, 2, callArgs.Count));

      foreach (var ns in ResolveExpression(callArgs[0])) {
        var toRemove = ns as NameSegment;
        foreach (var list in ResolveExpression(callArgs[1])) {
          var source = list as List<IVariable>;
          var target = source.FirstOrDefault(x => x.Name == toRemove.Name);
          if (target != null)
            source.Remove(target);

          yield return AddNewLocal(lv, source);
        }
      }
    }
  }
}
