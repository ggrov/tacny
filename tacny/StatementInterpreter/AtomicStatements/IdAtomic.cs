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
    class IdAtomic : Atomic, IAtomicStmt
    {
        public IdAtomic(Atomic atomic) : base(atomic) { }


        /// <summary>
        /// Evaluate a statement to true
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Assert(st is VarDeclStmt);
            List<Expression> args = null;
            IVariable lv = null;
            InitArgs(st, out lv, out args);
            Dafny.LiteralExpr lit = new Dafny.LiteralExpr(st.Tok, true);
            localContext.AddLocal(lv, lit);
            return null;
        }


    }
}
