using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Tacny;
using Util;

namespace LazyTacny {
  class IfGuardAtomic : Atomic, IAtomicLazyStmt {
    public IfGuardAtomic(Atomic ac) : base(ac) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> callArgs = null;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(callArgs, 0), Error.MkErr(st, 0, 0, callArgs.Count));
      List<Expression> guards = null;
      // was called from a ws tactic
      if (DynamicContext.whileStmt != null) {
        foreach (var wst in DynamicContext.whileStmt.Body.Body) {
          if (wst is IfStmt)
            guards = ExtractGuard(wst as IfStmt);
        }
      } else {
        var md = DynamicContext.md as Method;
        //Contract.Assert(md != null, Util.Error.MkErr(st, 23, "method"));
        foreach (var wst in md?.Body.Body) {
          if (wst is IfStmt)
            guards = ExtractGuard(wst as IfStmt);
        }
      }

      yield return AddNewLocal(lv, guards);
    }



    private static List<Expression> ExtractGuard(IfStmt statement) {
      Contract.Requires(statement != null);
      List<Expression> guardList = new List<Expression>();

      guardList.Add(statement.Guard);
      if (statement.Els != null && statement.Els is IfStmt) {
        guardList.AddRange(ExtractGuard(statement.Els as IfStmt));
      }

      return guardList;
    }
  }
}
