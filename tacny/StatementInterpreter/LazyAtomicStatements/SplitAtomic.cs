using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Tacny;
namespace LazyTacny {

  public class SplitAtomic : Atomic, IAtomicLazyStmt {


    public SplitAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return Split(st);
    }

    private IEnumerable<Solution> Split(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      List<Expression> result = new List<Expression>();
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 2, Util.Error.MkErr(st, 0, 2, callArgs.Count));
      foreach (var arg1 in ResolveExpression(callArgs[0])) {
        var expression = arg1 as BinaryExpr;
        if (expression == null)
          break;
        foreach (var arg2 in ResolveExpression(callArgs[1])) {
          var litVal = arg2 as LiteralExpr;
          Contract.Assert(litVal != null);
          var op = StringToOp(litVal.Value.ToString());
          result = SplitExpression(op, expression);
          yield return AddNewLocal(lv, result);
        }
      }
    }

    private List<Expression> SplitExpression(BinaryExpr.Opcode op, BinaryExpr expression) {
      var expList = new List<Expression>();

      if (!expression.Op.Equals(op)) {
        expList.Add(expression);
      } else if (IsChained(expression) && op.Equals(BinaryExpr.Opcode.And)) {
        expList.Add(expression);
      } else {
        if (!(expression.E0 is BinaryExpr))
          expList.Add(expression.E0);
        else {
          expList.AddRange(SplitExpression(op, expression.E0 as BinaryExpr));
        }
        if (!(expression.E1 is BinaryExpr))
          expList.Add(expression.E1);
        else {
          expList.AddRange(SplitExpression(op, expression.E1 as BinaryExpr));
        }
      }
      return expList;
    }


    private bool IsChained(BinaryExpr expression) {
      if (!expression.Op.Equals(BinaryExpr.Opcode.And))
        return false;
      var lhs = expression.E0 as BinaryExpr;
      var rhs = expression.E1 as BinaryExpr;
      if (lhs == null || rhs == null)
        return false;

      return BinaryExpr.IsEqualityOp(lhs.Op) && BinaryExpr.IsEqualityOp(rhs.Op);

    }
  }
}