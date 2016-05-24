using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny {
  class OperatorAtomic : Atomic, IAtomicLazyStmt {

    private enum MapType {
      UNDEFINED,
      OP,
      VAR,
    }
    public OperatorAtomic(Atomic atomic) : base(atomic) { }


    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return ReplaceOperator(st);
    }

    private IEnumerable<Solution> ReplaceOperator(Statement st) {
      IVariable lv = null;
      List<Expression> call_arguments = null;
      IEnumerable<Expression> enumerator = null;
      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 3, call_arguments.Count));
      var mapList = new List<MapDisplayExpr>();

      foreach (var arg2 in ResolveExpression(call_arguments[1])) {
        var operatorMap = arg2 as MapDisplayExpr;
        Contract.Assert(operatorMap != null, Util.Error.MkErr(st, 1, "Map"));
        mapList.Add(operatorMap);
      }

      foreach (var arg1 in ResolveExpression(call_arguments[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Util.Error.MkErr(st, 1, "Expression"));
        ExpressionTree et = ExpressionTree.ExpressionToTree(expression);

        foreach (var operatorMap in mapList) {
          switch (GetMapType(operatorMap)) {
            case MapType.UNDEFINED:
              Contract.Assert(false, Util.Error.MkErr(st, 1, "Operator or variable map"));
              break;
            case MapType.OP:
              enumerator = SubstOp(et, operatorMap);
              break;
            case MapType.VAR:
              enumerator = SubstVar(et, operatorMap);
              break;
            default:
              Contract.Assert(false, Util.Error.MkErr(st, 1, "Operator or variable map"));
              break;
          }

          foreach (var result in enumerator) {
            yield return AddNewLocal(lv, result);
          }
        }
      }
    }

    private IEnumerable<Expression> SubstVar(ExpressionTree formula, MapDisplayExpr args) {
      var varMap = new Dictionary<Expression, List<Expression>>();
      foreach (var pair in args.Elements) {
        Contract.Assert(pair.A is NameSegment, Util.Error.MkErr(formula.TreeToExpression(), 1, "Variable"));
        foreach (var p1 in ResolveExpression(pair.A)) {
          Expression key = null;
          if (p1 is Expression)
            key = p1 as Expression;
          else if (p1 is IVariable)
            key = IVariableToExpression(p1 as IVariable);
          else
            Contract.Assert(false, "Sum Tin Wong");
          Contract.Assert(key != null, Util.Error.MkErr(formula.TreeToExpression(), 1, "Variable"));
          var tempList = new List<Expression>();
          foreach (var p2 in ResolveExpression(pair.B)) {
            tempList.Add(p2 is IVariable ? IVariableToExpression(p2 as IVariable) : p2 as Expression);
          }
          varMap.Add(key, tempList);
        }
      }
      return ReplaceVar(varMap, formula);
    }

    private IEnumerable<Expression> ReplaceVar(Dictionary<Expression, List<Expression>> vars, ExpressionTree expression) {
      if (expression == null)
        yield break;
      if (expression.root == null)
        expression.SetRoot();

      if (expression.IsLeaf()) {
        foreach (var kvp in vars) {
          
          if (SingletonEquality(expression.data, kvp.Key)) {
            foreach (var var in kvp.Value) {
              // safeguard against infinite loop
              if (SingletonEquality(kvp.Key, var))
                continue;
              //if (!ValidateType(var, expression.parent.TreeToExpression() as BinaryExpr))
              //   continue;
              var newVal = ExpressionTree.ExpressionToTree(var);
              var copy = expression.root.Copy();
              var newTree = ExpressionTree.FindAndReplaceNode(copy, newVal, expression.Copy());
              yield return newTree.root.TreeToExpression();
              foreach (var item in ReplaceVar(vars, newTree.root))
                yield return item;
            }
          }
        }
      } else {
        foreach (var item in ReplaceVar(vars, expression.lChild))
          yield return item;
        foreach (var item in ReplaceVar(vars, expression.rChild))
          yield return item;
      }
    }

    private IEnumerable<Expression> SubstOp(ExpressionTree formula, MapDisplayExpr args) {
      var opcodeMap = new Dictionary<BinaryExpr.Opcode, BinaryExpr.Opcode>();

      foreach (var pair in args.Elements) {
        var op1String = pair.A as LiteralExpr;
        Contract.Assert(op1String != null);
        var op2String = pair.B as LiteralExpr;
        Contract.Assert(op2String != null);
        opcodeMap.Add(StringToOp(op1String.Value.ToString()), StringToOp(op2String.Value.ToString()));
      }
      return ReplaceOp(opcodeMap, formula);
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

    private MapType GetMapType(MapDisplayExpr mde) {
      var element = mde.Elements[0];

      if (element.A.GetType() != element.B.GetType())
        return MapType.UNDEFINED;
      else if (element.A.GetType() == typeof(StringLiteralExpr))
        return MapType.OP;
      else if (element.A.GetType() == typeof(NameSegment))
        return MapType.VAR;
      else
        return MapType.UNDEFINED;

    }

    private bool HasExpression(Expression constant, List<Expression> constants) {

      if (!(constant is LiteralExpr || constant is ExprDotName || constant is NameSegment))
        return false;

      return constants.Exists(j => SingletonEquality(j, constant));
    }

  }
}
