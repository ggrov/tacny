using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny {
  class OperatorAtomic : Atomic, IAtomicLazyStmt {
    public OperatorAtomic(Atomic atomic) : base(atomic) { }


    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return ReplaceOperator(st);
    }

    private IEnumerable<Solution> ReplaceOperator(Statement st) {
      IVariable lv = null;
      List<Expression> call_arguments = null;

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 3, call_arguments.Count));

      foreach (var arg1 in ResolveExpression(call_arguments[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Util.Error.MkErr(st, 1, "Expression"));
        foreach (var arg2 in ResolveExpression(call_arguments[1])) {
          var operatorMap = arg2 as MapDisplayExpr;
          Contract.Assert(operatorMap != null, Util.Error.MkErr(st, 1, "Map"));
          ExpressionTree et = ExpressionTree.ExpressionToTree(expression);
          List<Expression> exp_list = new List<Expression>();
          var opcodeMap = new Dictionary<BinaryExpr.Opcode, BinaryExpr.Opcode>();
          foreach (var pair in operatorMap.Elements) {
            var op1String = pair.A as LiteralExpr;
            Contract.Assert(op1String != null);
            var op2String = pair.B as LiteralExpr;
            Contract.Assert(op2String != null);
            opcodeMap.Add(StringToOp(op1String.Value.ToString()), StringToOp(op2String.Value.ToString()));
          }
          foreach (var result in ReplaceOp(opcodeMap, et)) {
            yield return AddNewLocal(lv, result);
          }
        }
      }
    }

    protected IEnumerable<Expression> ReplaceOp(Dictionary<BinaryExpr.Opcode, BinaryExpr.Opcode> opCodeMap, ExpressionTree formula) {

      if (formula == null || formula.IsLeaf())
        yield break;

      var expt = formula;
      if (formula.data is BinaryExpr) {
        var bexp = formula.data as BinaryExpr;
        if (opCodeMap.ContainsKey(bexp.Op)) {
          expt = expt.Copy();

          expt.data = new BinaryExpr(formula.data.tok, opCodeMap[bexp.Op], bexp.E0, bexp.E1);
          expt = expt.root;
          yield return expt.root.TreeToExpression();
        }
      }
      foreach (var item in ReplaceOp(opCodeMap, expt.lChild))
        yield return item;
      foreach (var item in ReplaceOp(opCodeMap, expt.rChild))
        yield return item;
    }
  }
}
