using System.Diagnostics.Contracts;
using System.Numerics;
using Microsoft.Dafny;

namespace Tacny
{
    class BlockAtomic : Atomic
    {
        protected ExpressionTree guard;

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
            guard = ExpressionTree.ExpressionToTree(guard_wrapper);
          
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
            return IsResolvable(guard);
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
            if (expt.IsLeaf())
            {
                LiteralExpr lit = EvaluateLeaf(expt) as LiteralExpr;
                return lit.Value is bool ? (bool)lit.Value : false;
            }
            // left branch only
          if (expt.LChild != null && expt.RChild == null)
            return EvaluateGuardTree(expt.LChild);
          // if there is no more nesting resolve the expression
          if (expt.LChild.IsLeaf() && expt.RChild.IsLeaf())
          {
            LiteralExpr lhs = null;
            LiteralExpr rhs = null;
            lhs = EvaluateLeaf(expt.LChild) as LiteralExpr;
            rhs = EvaluateLeaf(expt.RChild) as LiteralExpr;
            if (!lhs.GetType().Equals(rhs.GetType()))
              return false;
            BinaryExpr bexp = tcce.NonNull(expt.Data as BinaryExpr);
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
            if (bexp.Op == BinaryExpr.Opcode.Neq)
              return res != 0;
            if (bexp.Op == BinaryExpr.Opcode.Ge)
              return res >= 0;
            if (bexp.Op == BinaryExpr.Opcode.Gt)
              return res > 0;
            if (bexp.Op == BinaryExpr.Opcode.Le)
              return res <= 0;
            if (bexp.Op == BinaryExpr.Opcode.Lt)
              return res < 0;
          }
          else // evaluate a nested expression
          {
            BinaryExpr bexp = tcce.NonNull(expt.Data as BinaryExpr);
            if (bexp.Op == BinaryExpr.Opcode.And)
              return EvaluateGuardTree(expt.LChild) && EvaluateGuardTree(expt.RChild);
            if (bexp.Op == BinaryExpr.Opcode.Or)
              return EvaluateGuardTree(expt.LChild) || EvaluateGuardTree(expt.RChild);
          }
          return false;
        }
    }
}
