using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using System.Numerics;


namespace Tacny
{
    class BlockAtomic : Atomic
    {
        protected ExpressionTree guard = null;

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
            // extract the guard statement
            if ((ifStmt = st as IfStmt) != null)
                guard_wrapper = ifStmt.Guard;
            else if ((whileStmt = st as WhileStmt) != null)
                guard_wrapper = whileStmt.Guard;
            else
                return null;
            this.guard = ExpressionTree.ExpressionToTree(guard_wrapper);
          
            return guard_wrapper;
        }

        /// <summary>
        /// Determine whether the guard is resolvable
        /// Guard is resolvable if both sides of the equation are litteral expressions
        /// </summary>
        /// <param name="guard"></param>
        /// <returns></returns>
        [Pure]
        protected bool IsResolvable()
        {
            return IsResolvable(this.guard);
        }

        /// <summary>
        /// Resolve an expression guard
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        protected bool EvaluateGuard()
        {
            Contract.Requires(guard != null);
            Contract.Requires(IsResolvable());
            return EvaluateGuardTree(guard);
        }
        /// <summary>
        /// Evalutate an expression tree
        /// </summary>
        /// <param name="expt"></param>
        /// <returns></returns>
        protected bool EvaluateGuardTree(ExpressionTree expt)
        {
            Contract.Requires(expt != null);
            // if the node is leaf, cast it to bool and return
            if (expt.isLeaf())
            {
                Dafny.LiteralExpr lit = EvaluateLeaf(expt) as Dafny.LiteralExpr;
                return lit.Value is bool ? (bool)lit.Value : false;
            }
            // left branch only
            else if (expt.lChild != null && expt.rChild == null)
                return EvaluateGuardTree(expt.lChild);
              // if there is no more nesting resolve the expression
            else if (expt.lChild.isLeaf() && expt.rChild.isLeaf())
            {
                Dafny.LiteralExpr lhs = null;
                Dafny.LiteralExpr rhs = null;
                lhs = EvaluateLeaf(expt.lChild) as Dafny.LiteralExpr;
                rhs = EvaluateLeaf(expt.rChild) as Dafny.LiteralExpr;
                if (!lhs.GetType().Equals(rhs.GetType()))
                    return false;
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
                int res = -1;
                if (lhs.Value is BigInteger)
                {
                    BigInteger l = (BigInteger)lhs.Value;
                    BigInteger r = (BigInteger)rhs.Value;
                    res = l.CompareTo(r);
                }
                else if (lhs.Value is string)
                {
                    string l = lhs.Value as string;
                    string r = rhs.Value as string;
                    res = l.CompareTo(r);
                }
                else if (lhs.Value is bool)
                    res = ((bool)lhs.Value).CompareTo((bool)rhs.Value);

                if (bexp.Op == BinaryExpr.Opcode.Eq)
                    return res == 0;
                else if (bexp.Op == BinaryExpr.Opcode.Neq)
                    return res != 0;
                else if (bexp.Op == BinaryExpr.Opcode.Ge)
                    return res >= 0;
                else if (bexp.Op == BinaryExpr.Opcode.Gt)
                    return res > 0;
                else if (bexp.Op == BinaryExpr.Opcode.Le)
                    return res <= 0;
                else if (bexp.Op == BinaryExpr.Opcode.Lt)
                    return res < 0;
            }
            else // evaluate a nested expression
            {
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
                if (bexp.Op == BinaryExpr.Opcode.And)
                    return EvaluateGuardTree(expt.lChild) && EvaluateGuardTree(expt.rChild);
                else if (bexp.Op == BinaryExpr.Opcode.Or)
                    return EvaluateGuardTree(expt.lChild) || EvaluateGuardTree(expt.rChild);
            }
            return false;
        }
    }
}
