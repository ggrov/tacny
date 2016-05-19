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

    public IEnumerable<Solution> CreateInvar(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> call_arguments = null;
      Expression formula = null;
      MaybeFreeExpression invariant = null;

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));


      foreach (var forlumla in ResolveExpression(call_arguments[0])) {
        Contract.Assert(formula != null);
        invariant = new MaybeFreeExpression(formula);
        var ac = this.Copy();
        ac.AddLocal(lv, invariant);
        yield return new Solution(this.Copy());
      }
      yield break;
    }

    public IEnumerable<Solution> AddInvar(TacticInvariantStmt st, Solution solution) {
      MaybeFreeExpression invariant = null;
      MaybeFreeExpression[] invar_arr = null;
      List<MaybeFreeExpression> invar = null;
      
      
      foreach (var item in ResolveExpression(st.Expr)) {
        invariant = item as MaybeFreeExpression;
        if(invariant == null) {
          invariant = new MaybeFreeExpression(item as Expression);
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
        invar.Add(invariant);
        nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);
        yield return AddNewStatement<WhileStmt>(ws, nws);
      }
      yield break;
    }
  }
}
