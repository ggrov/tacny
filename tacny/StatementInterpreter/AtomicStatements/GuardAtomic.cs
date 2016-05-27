using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class GuardAtomic : Atomic, IAtomicStmt
    {
        public GuardAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            ExtractGuard(st, ref solution_list);
        }

        public void ExtractGuard(Statement st, ref List<Solution> solution_list)
        {
            WhileStmt ws = null;
            IVariable lv = null;
            Expression guard = null;
            List<Expression> call_arguments; // we don't care about this

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));

            ws = FindWhileStmt(globalContext.tac_call, globalContext.md);
            Contract.Assert(ws != null, Error.MkErr(st, 11));
            guard = ws.Guard;

            AddLocal(lv, guard);
            solution_list.Add(new Solution(Copy()));
        }

    }
}
