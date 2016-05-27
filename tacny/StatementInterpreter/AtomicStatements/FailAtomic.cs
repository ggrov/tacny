using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace Tacny
{
    class FailAtomic : Atomic, IAtomicStmt
    {
        public FailAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Assert(st is TacticVarDeclStmt);
            List<Expression> args = null;
            IVariable lv = null;
            InitArgs(st, out lv, out args);
            LiteralExpr lit = new LiteralExpr(st.Tok, false);
            localContext.AddLocal(lv, lit);
        }
    }
}
