using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Tacny;
namespace LazyTacny {

  public class PredicateAtomic : Atomic, IAtomicLazyStmt {


    public PredicateAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return GetPredicates(st);
    }


    private IEnumerable<Solution> GetPredicates(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      var result = new List<MemberDecl>();
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 0, Util.Error.MkErr(st, 0, 0, callArgs.Count));

      foreach(var member in StaticContext.program.members.Values) {
        var pred = member as Predicate;
        if (pred != null)
          result.Add(pred);
      }

      yield return AddNewLocal(lv, result);
    }
  }
}