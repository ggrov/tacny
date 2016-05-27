using System.Collections.Generic;
using Microsoft.Dafny;

namespace Tacny
{
    class IdAtomic : Atomic, IAtomicStmt
    {
        public IdAtomic(Atomic atomic) : base(atomic) { }


        /// <summary>
        /// Evaluate a statement to true
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> args = null;
            IVariable lv = null;
            InitArgs(st, out lv, out args);
            LiteralExpr lit = new LiteralExpr(st.Tok, true);
            localContext.AddLocal(lv, lit);
        }


    }
}
