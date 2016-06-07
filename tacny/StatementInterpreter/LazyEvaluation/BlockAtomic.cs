using System;
using System.Diagnostics.Contracts;
using System.Numerics;
using Microsoft.Dafny;

namespace LazyTacny {
  public class BlockAtomic : Atomic {
    protected ExpressionTree Guard;

    protected BlockAtomic(Atomic atomic) : base(atomic) { }

    /// <summary>
    /// Extract the loop guard from the statement
    /// </summary>
    /// <param name="st"></param>
    /// <returns></returns>
    protected Expression ExtractGuard(Statement st) {
      Contract.Requires(st != null);

      IfStmt ifStmt;
      WhileStmt whileStmt;
      Expression guardWrapper;
      // extract the guard statement
      if ((ifStmt = st as IfStmt) != null)
        guardWrapper = ifStmt.Guard;
      else if ((whileStmt = st as WhileStmt) != null)
        guardWrapper = whileStmt.Guard;
      else
        return null;
      Guard = ExpressionTree.ExpressionToTree(guardWrapper);

      return guardWrapper;
    }

    /// <summary>
    /// Determine whether the guard is resolvable
    /// Guard is resolvable if both sides of the equation are litteral expressions
    /// </summary>
    /// <returns></returns>
    [Pure]
    protected bool IsResolvable() {
      return IsResolvable(Guard);
    }

    /// <summary>
    /// Resolve an expression guard
    /// </summary>
    /// <returns></returns>
    protected bool EvaluateGuard() {
      Contract.Requires(Guard != null);
      Contract.Requires(IsResolvable());
      return EvaluateGuardTree(Guard);
    }
    /// <summary>
    /// Evalutate an expression tree
    /// </summary>
    /// <param name="expt"></param>
    /// <returns></returns>
    protected bool EvaluateGuardTree(ExpressionTree expt) {
      Contract.Requires(expt != null);
      // if the node is leaf, cast it to bool and return
      if (expt.IsLeaf()) {
        var lit = EvaluateLeaf(expt) as LiteralExpr;
        return lit?.Value is bool && (bool)lit.Value;
      }
      // left branch only
      if (expt.LChild != null && expt.RChild == null)
        return EvaluateGuardTree(expt.LChild);
      // if there is no more nesting resolve the expression
      if (expt.LChild.IsLeaf() && expt.RChild.IsLeaf()) {
        LiteralExpr lhs = null;
        LiteralExpr rhs = null;
        lhs = EvaluateLeaf(expt.LChild) as LiteralExpr;
        rhs = EvaluateLeaf(expt.RChild) as LiteralExpr;
        if (lhs.GetType() == rhs.GetType())
          return false;
        var bexp = tcce.NonNull(expt.Data as BinaryExpr);
        int res = -1;
        if (lhs.Value is BigInteger) {
          var l = (BigInteger)lhs.Value;
          var r = (BigInteger)rhs.Value;
          res = l.CompareTo(r);
        } else if (lhs.Value is string) {
          var l = (string) lhs.Value;
          var r = rhs.Value as string;
          res = String.Compare(l, r, StringComparison.Ordinal);
        } else if (lhs.Value is bool) {
          res = ((bool)lhs.Value).CompareTo((bool)rhs.Value);
        }

        switch (bexp.Op) {
          case BinaryExpr.Opcode.Eq:
            return res == 0;
          case BinaryExpr.Opcode.Neq:
            return res != 0;
          case BinaryExpr.Opcode.Ge:
            return res >= 0;
          case BinaryExpr.Opcode.Gt:
            return res > 0;
          case BinaryExpr.Opcode.Le:
            return res <= 0;
          case BinaryExpr.Opcode.Lt:
            return res < 0;
        }
      } else // evaluate a nested expression
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
