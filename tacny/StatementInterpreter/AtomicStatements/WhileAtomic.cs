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
    class WhileAtomic : Atomic, IAtomicStmt
    {
        public WhileAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            BinaryExpr guard = ExtractGuard(st);
            if(guard == null)
                return "Unable to extract the while statement guard";
            if (IsResolvable(guard))
            {
                return ExecuteLoop(st as WhileStmt, ref solution_list);
            }

            return null;
        }


        private string ExecuteLoop(WhileStmt loop, ref List<Solution> solution_list)
        {


            return null;
        }

        /// <summary>
        /// Extract the loop guard from the statement
        /// </summary>
        /// <param name="st"></param>
        /// <returns></returns>
        private BinaryExpr ExtractGuard(Statement st)
        {
            Contract.Requires(st != null);
            IfStmt ifStmt = null;
            WhileStmt whileStmt = null;
            Expression guard_wrapper = null;
            BinaryExpr guard = null;
            // extract the guard statement
            if ((ifStmt = st as IfStmt) != null)
                guard_wrapper = ifStmt.Guard;
            else if ((whileStmt = st as WhileStmt) != null)
                guard_wrapper = whileStmt.Guard;
            else
                return null;
            // check if guard is in parenthesis
            ParensExpression parens = null;
            if ((parens = guard_wrapper as ParensExpression) != null)
                guard = parens.E as BinaryExpr;
            else
                guard = guard_wrapper as BinaryExpr;

            return guard;
        }

        /// <summary>
        /// Determine whether the guard is resolvable
        /// Guard is resolvable if both sides of the equation are litteral expressions
        /// </summary>
        /// <param name="guard"></param>
        /// <returns></returns>
        private bool IsResolvable(BinaryExpr guard)
        {
            /**
             * for simplicity sake assume taht binary expression will be in a form of x < y
             */
            NameSegment lhsNs = guard.E0 as NameSegment;
            if (lhsNs == null)
                return false;
            NameSegment rhsNs = guard.E1 as NameSegment;
            if (rhsNs == null)
                return false;


            object val1 = GetLocalValueByName(lhsNs);
            object val2 = GetLocalValueByName(rhsNs);
            return val1 is Dafny.LiteralExpr && val2 is Dafny.LiteralExpr;
        }
    }
}
