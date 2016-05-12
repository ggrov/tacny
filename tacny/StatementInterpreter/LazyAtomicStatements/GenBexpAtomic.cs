using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace LazyTacny {
  class GenBexpAtomic : Atomic, IAtomicLazyStmt {
    public GenBexpAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      yield return (GenerateExpression(st, solution));
      yield break;
    }


    private Solution GenerateExpression(Statement st, Solution solution) {
      IVariable lv = null;
      List<Expression> callArguments = null;
      InitArgs(st, out lv, out callArguments);
      BinaryExpr bexp = null;
      foreach (var lhsValue in ResolveExpression(callArguments[0])) {
        Expression lhs = null;
        if (lhsValue is Expression)
          lhs = lhsValue as Expression;
        else if (lhsValue is IVariable)
          lhs = IVariableToExpression(lhsValue as IVariable);
        foreach (var rhsValue in ResolveExpression(callArguments[2])) {
          Expression rhs = null;
          if (rhsValue is Expression)
            rhs = rhsValue as Expression;
          else if (rhsValue is IVariable)
            rhs = IVariableToExpression(rhsValue as IVariable);
          foreach (var op in ResolveExpression(callArguments[1])) {

            var opLiteral = op as StringLiteralExpr;
            var opString = opLiteral.Value.ToString();
            bexp = new BinaryExpr(st.Tok, ToOpCode(opString), lhs as Expression, rhs as Expression);
          }
        }
      }
      return AddNewLocal<BinaryExpr>(lv, bexp);
    }


    protected BinaryExpr.Opcode ToOpCode(string op) {
      foreach (BinaryExpr.Opcode code in Enum.GetValues(typeof(BinaryExpr.Opcode))) {
        try {
          if (BinaryExpr.OpcodeString(code) == op)
            return code;
        } catch (cce.UnreachableException) {
          throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
        }

      }
      throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
    }
  }
}
