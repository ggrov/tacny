using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Tacny;
using Util;

namespace LazyTacny {
  class LemmasAtomic : Atomic, IAtomicLazyStmt {
    public LemmasAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      yield return Lemmas(st);
    }

    private Solution Lemmas(Statement st) {
      IVariable lv;
      List<Expression> callArguments; // we don't care about this
      var lemmas = new List<MemberDecl>();

      InitArgs(st, out lv, out callArguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(callArguments, 0), Error.MkErr(st, 0, 0, callArguments.Count));


      foreach (var member in StaticContext.program.Members.Values) {
        var lem = member as Lemma;
        var fl = member as FixpointLemma;
        if (lem != null)
          lemmas.Add(lem);
        else if (fl != null)
          lemmas.Add(fl);
      }
      AddLocal(lv, lemmas);
      return new Solution(Copy());
    }
  }
}
