using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;

namespace LazyTacny {
  class PostcondAtomic : Atomic, IAtomicLazyStmt {
    public PostcondAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return Postcond(st);
    }


    private Solution Postcond(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs; // we don't care about this
      List<Expression> ensures = new List<Expression>();
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));

      Contract.Assert(callArgs.Count <= 1, Util.Error.MkErr(st, 0, 0, callArgs.Count));

      MemberDecl memberDecl = null;
      if (callArgs.Count > 0) {
        foreach (var member in ResolveExpression(callArgs[0])) {
          memberDecl = member as MemberDecl;
          if (memberDecl == null)
            Contract.Assert(false, Util.Error.MkErr(st, 1, "Function, [Ghost] Method, Declaration"));
          break;
        }
      } else {
        memberDecl = StaticContext.md;
      }

      Function fun = memberDecl as Function;
      if (fun != null) {
        foreach (var req in fun.Ens)
          ensures.Add(Util.Copy.CopyExpression(req));
      } else {

        Method method = memberDecl as Method;
        if (method != null) {
          foreach (var req in method.Ens)
            ensures.Add(Util.Copy.CopyExpression(req.E));
        } else {
          Contract.Assert(false, Util.Error.MkErr(st, 1, "Function, [Ghost] Method, Declaration"));
        }
      }
      return AddNewLocal(lv, ensures);
    }
  }
}
