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
    class BlockAtomic : Atomic
    {
        protected BlockAtomic(Atomic atomic) : base(atomic) { }

        /// <summary>
        /// Extract the loop guard from the statement
        /// </summary>
        /// <param name="st"></param>
        /// <returns></returns>
        protected Expression ExtractGuard(Statement st)
        {
            Contract.Requires(st != null);
            IfStmt ifStmt = null;
            WhileStmt whileStmt = null;
            Expression guard_wrapper = null;
            Expression guard = null;
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
                // if guard is not binary expr try literal
                if ((guard = parens.E as BinaryExpr) == null)
                    guard = parens.E as Dafny.LiteralExpr;
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
        protected bool IsResolvable(Expression expr)
        {
            NameSegment lhsNs = null;
            NameSegment rhsNs = null;
            Dafny.LiteralExpr llexp = null;
            Dafny.LiteralExpr rlexp = null;
            
            // check if the expression is a literal expression
            Dafny.LiteralExpr litGuard = expr as Dafny.LiteralExpr;
            if (litGuard != null)
                return true;
            /**
             * for simplicity sake assume that binary expression will be in a form of x < y
             */
            BinaryExpr guard = expr as BinaryExpr;
            lhsNs = guard.E0 as NameSegment;
            if (lhsNs == null)
            {
                // if lhs is not a binary expr try literal
                llexp = guard.E0 as Dafny.LiteralExpr;
            }

            rhsNs = guard.E1 as NameSegment;
            if (rhsNs == null)
            {//
                // if lhs is not a binary expr try literal
                rlexp = guard.E1 as Dafny.LiteralExpr;
            }
            if (rlexp != null && llexp != null)
                return true;

            object val1 = null;
            object val2 = null;
            if (lhsNs != null)
                val1 = GetLocalValueByName(lhsNs);
            if (rhsNs != null)
                val2 = GetLocalValueByName(rhsNs);
            // if there is a local definition for lhs
            if (val1 != null)
            {
                // if val2 is also defined, check whether both are litterals
                if (val2 != null)
                    return val1 is Dafny.LiteralExpr && val2 is Dafny.LiteralExpr;
                // if val2 not assigned check  type of val1 and if rlexp literal is not null
                return val1 is Dafny.LiteralExpr && rlexp != null;

            }
            else if (val2 != null)
            {
                return val2 is Dafny.LiteralExpr && llexp != null;
            }

            return false;
        }
    }
}
