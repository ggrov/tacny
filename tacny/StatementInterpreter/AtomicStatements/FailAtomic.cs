using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class FailAtomic : Atomic, IAtomicStmt
    {
        public FailAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> args = null;
            IVariable lv = null;
            InitArgs(st, out lv, out args);
            Dafny.LiteralExpr lit = new Dafny.LiteralExpr(st.Tok, false);
            localContext.AddLocal(lv, lit);
        }
    }
}
