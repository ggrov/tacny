using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;

namespace LazyTacny {
  class InvariantAtomic : Atomic, IAtomicLazyStmt {
    public InvariantAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      var temp = st as TacticInvariantStmt;

      if(temp.IsObjectLevel) {
        return AddInvar(temp, solution);
      } else {
        throw new NotImplementedException();
      }
    }
    public IEnumerable<Solution> AddInvar(TacticInvariantStmt st, Solution solution) {
      MaybeFreeExpression invariant = null;
      MaybeFreeExpression[] invar_arr = null;
      List<MaybeFreeExpression> invar = null;
      
      
      foreach (var item in ResolveExpression(st.Expr)) {
        if (item is UpdateStmt) {
          var us = item as UpdateStmt;
          var aps = ((ExprRhs)us.Rhss[0]).Expr as ApplySuffix;
          if (aps != null)
            invariant = new MaybeFreeExpression(aps);
        } else {
          invariant = item as MaybeFreeExpression;
          if (invariant == null) {
            invariant = new MaybeFreeExpression(item as Expression);
          }
        }
        WhileStmt nws = null;

        WhileStmt ws = DynamicContext.whileStmt;
        Contract.Assert(ws != null, Util.Error.MkErr(st, 11));
        // if we already added new invariants to the statement, use the updated statement instead
        nws = GetUpdated(ws) as WhileStmt;

        if (nws != null)
          invar_arr = nws.Invariants.ToArray();
        else
          invar_arr = ws.Invariants.ToArray();

        invar = new List<MaybeFreeExpression>(invar_arr);
        invar.Insert(0, invariant);
        //invar.Add(invariant);
        nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);
        yield return AddNewStatement<WhileStmt>(ws, nws);
      }
      yield break;
    }
  }
}
