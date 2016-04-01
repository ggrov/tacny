using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;
namespace LazyTacny
{
    class GuardAtomic : Atomic, IAtomicLazyStmt
    {
        public GuardAtomic(Atomic atomic) : base(atomic) { }


        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            WhileStmt ws = null;
            IVariable lv = null;
            Expression guard = null;
            List<Expression> call_arguments; // we don't care about this

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            ws = FindWhileStmt(globalContext.tac_call, globalContext.md);
            Contract.Assert(ws != null, Util.Error.MkErr(st, 11));
            guard = ws.Guard;
            var ac = this.Copy();
            ac.AddLocal(lv, guard);
            yield return new Solution(ac);
            yield break;
        }
    }
}
