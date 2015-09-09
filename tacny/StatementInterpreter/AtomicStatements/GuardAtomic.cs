using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;


namespace Tacny
{
    class GuardAtomic : Atomic, IAtomicStmt
    {
        public GuardAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return ExtractGuard(st, ref solution_list);
        }

        public string ExtractGuard(Statement st, ref List<Solution> solution_list)
        {
            WhileStmt ws = null;
            IVariable lv = null;
            Expression guard = null;
            string err;
            List<Expression> call_arguments; // we don't care about this

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "extract_guard: " + err;


            ws = FindWhileStmt(globalContext.tac_call, globalContext.md);
            if (ws == null)
                return "extract_guard: extract_guard can only be called from a while loop";
            guard = ws.Guard;

            AddLocal(lv, guard);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }

    }
}
