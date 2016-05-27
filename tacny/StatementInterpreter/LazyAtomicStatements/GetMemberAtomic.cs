using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  public class GetMemberAtomic : Atomic, IAtomicLazyStmt {
    public GetMemberAtomic(Atomic atomic) : base(atomic) { }
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return GetMember(st, solution);
    }

    private Solution GetMember(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> call_arguments;
      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(call_arguments.Count <= 1, Error.MkErr(st, 0, 1, call_arguments.Count));
      MemberDecl memberDecl = null;
      if (call_arguments.Count == 1) {
        var memberName = call_arguments[0] as StringLiteralExpr;
        try {
          memberDecl = StaticContext.program.Members[memberName.AsStringLiteral()];
        } catch (KeyNotFoundException e) {
          Console.Out.WriteLine(e.Message);
          Contract.Assert(false, Error.MkErr(st, 20, memberName.AsStringLiteral()));
        }
      } else {
        memberDecl = StaticContext.md;
      }
      return AddNewLocal(lv, memberDecl);
    }
  }
}
