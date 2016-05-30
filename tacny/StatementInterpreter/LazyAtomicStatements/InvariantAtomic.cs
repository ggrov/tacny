using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class InvariantAtomic : Atomic, IAtomicLazyStmt {
    public InvariantAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      var temp = st as TacticInvariantStmt;

      if(temp != null && temp.IsObjectLevel) {
        return AddInvar(temp, solution);
      }
      throw new NotImplementedException();
    }
    public IEnumerable<Solution> AddInvar(TacticInvariantStmt st, Solution solution) {
      MaybeFreeExpression invariant = null;


      foreach (var item in ResolveExpression(st.Expr)) {
        if (item is UpdateStmt) {   
          var us = item as UpdateStmt;
          var aps = ((ExprRhs)us.Rhss[0]).Expr as ApplySuffix;
          if (aps != null)
            invariant = new MaybeFreeExpression(aps);
        } else {
          invariant = item as MaybeFreeExpression ?? new MaybeFreeExpression(item as Expression);
        }
        WhileStmt nws = null;

        var ws = DynamicContext.whileStmt;
        Contract.Assert(ws != null, Error.MkErr(st, 11));
        // if we already added new invariants to the statement, use the updated statement instead
        nws = GetUpdated(ws) as WhileStmt;

        var invarArr = nws?.Invariants.ToArray() ?? ws.Invariants.ToArray();

        var invar = new List<MaybeFreeExpression>(invarArr) {invariant};
        //invar.Insert(0, invariant);
        nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);
        yield return AddNewStatement(ws, nws);
      }
    }
  }
}
