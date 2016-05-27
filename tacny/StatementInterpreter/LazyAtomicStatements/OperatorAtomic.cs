using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Tacny;
using Util;

namespace LazyTacny {
  class OperatorAtomic : Atomic, IAtomicLazyStmt {

    private enum MapType {
      Undefined,
      Op,
      Var
    }
    public OperatorAtomic(Atomic atomic) : base(atomic) { }


    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return ReplaceOperator(st);
    }

    private IEnumerable<Solution> ReplaceOperator(Statement st) {
      IVariable lv;
      List<Expression> callArguments;
      IEnumerable<Expression> enumerator = null;
      InitArgs(st, out lv, out callArguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(callArguments, 2), Error.MkErr(st, 0, 3, callArguments.Count));
      var mapList = new List<MapDisplayExpr>();

      foreach (var arg2 in ResolveExpression(callArguments[1])) {
        var operatorMap = arg2 as MapDisplayExpr;
        Contract.Assert(operatorMap != null, Error.MkErr(st, 1, "Map"));
        mapList.Add(operatorMap);
      }

      foreach (var arg1 in ResolveExpression(callArguments[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Error.MkErr(st, 1, "Expression"));
        var et = ExpressionTree.ExpressionToTree(expression);

        foreach (var operatorMap in mapList) {
          switch (GetMapType(operatorMap)) {
            case MapType.Undefined:
              Contract.Assert(false, Error.MkErr(st, 1, "Operator or variable map"));
              break;
            case MapType.Op:
              enumerator = SubstOp(et, operatorMap);
              break;
            case MapType.Var:
              enumerator = SubstVar(et, operatorMap);
              break;
            default:
              Contract.Assert(false, Error.MkErr(st, 1, "Operator or variable map"));
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
        Contract.Assert(pair.A is NameSegment, Error.MkErr(formula.TreeToExpression(), 1, "Variable"));
        foreach (var p1 in ResolveExpression(pair.A)) {
          Expression key = null;
          if (p1 is Expression)
            key = p1 as Expression;
          else if (p1 is IVariable)
            key = VariableToExpression(p1 as IVariable);
          else
            Contract.Assert(false, "Sum Tin Wong");
          Contract.Assert(key != null, Error.MkErr(formula.TreeToExpression(), 1, "Variable"));
          var tempList = ResolveExpression(pair.B).Select(p2 => p2 is IVariable ? VariableToExpression((IVariable) p2) : p2 as Expression).ToList();
          if (key != null)
            varMap.Add(key, tempList);
        }
      }
      return ReplaceVar(varMap, formula);
    }

    private IEnumerable<Expression> ReplaceVar(Dictionary<Expression, List<Expression>> vars, ExpressionTree expression) {
      if (expression == null)
        yield break;
      if (expression.Root == null)
        expression.SetRoot();

      if (expression.IsLeaf()) {
        foreach (var kvp in vars) {

          if (SingletonEquality(expression.Data, kvp.Key)) {
            foreach (var var in kvp.Value) {
              // safeguard against infinite loop
              if (SingletonEquality(kvp.Key, var))
                continue;
              ExpressionTree newVal = RewriteExpression(expression, kvp.Key, var);
              if (newVal == null)
                break;
              var copy = expression.Root.Copy();
              var newTree = ExpressionTree.FindAndReplaceNode(copy, newVal, expression.Copy());
              yield return newTree.Root.TreeToExpression();
              foreach (var item in ReplaceVar(vars, newTree.Root))
                yield return item;
            }
          }
        }
      } else {
        foreach (var item in ReplaceVar(vars, expression.LChild))
          yield return item;
        foreach (var item in ReplaceVar(vars, expression.RChild))
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
        opcodeMap.Add(StringToOp(op1String?.Value.ToString()), StringToOp(op2String?.Value.ToString()));
      }
      return ReplaceOp(opcodeMap, formula);
    }



    protected IEnumerable<Expression> ReplaceOp(Dictionary<BinaryExpr.Opcode, BinaryExpr.Opcode> opCodeMap, ExpressionTree formula) {

      if (formula == null || formula.IsLeaf())
        yield break;

      var expressionTree = formula;
      var binaryExpr = formula.Data as BinaryExpr;
      if (binaryExpr != null) {
        var bexp = binaryExpr;
        if (opCodeMap.ContainsKey(bexp.Op)) {
          expressionTree = expressionTree.Copy();

          expressionTree.Data = new BinaryExpr(binaryExpr.tok, opCodeMap[bexp.Op], bexp.E0, bexp.E1);
          expressionTree = expressionTree.Root;
          yield return expressionTree.Root.TreeToExpression();
        }
      }
      foreach (var item in ReplaceOp(opCodeMap, expressionTree.LChild))
        yield return item;
      foreach (var item in ReplaceOp(opCodeMap, expressionTree.RChild))
        yield return item;
    }

    private MapType GetMapType(MapDisplayExpr mde) {
      var element = mde.Elements[0];

      if (element.A.GetType() != element.B.GetType())
        return MapType.Undefined;
      if (element.A.GetType() == typeof(StringLiteralExpr))
        return MapType.Op;
      if (element.A.GetType() == typeof(NameSegment))
        return MapType.Var;
      return MapType.Undefined;
    }

/*
    private bool HasExpression(Expression constant, List<Expression> constants) {

      if (!(constant is LiteralExpr || constant is ExprDotName || constant is NameSegment))
        return false;

      return constants.Exists(j => SingletonEquality(j, constant));
    }
*/



    private ExpressionTree RewriteExpression(ExpressionTree expt, Expression key, Expression value) {
      if (key is LiteralExpr || key is ExprDotName)
        return new ExpressionTree(value);
      var segment = key as NameSegment;
      if (segment != null) {
        var expr = expt.TreeToExpression();
        var ac = Copy();
        ac.AddLocal(new LocalVariable(segment.tok, segment.tok, segment.Name, new ObjectType(), true), value);
        foreach (var item in ac.ResolveExpression(expr))
        {
          if (item is Expression) {
            return new ExpressionTree(item as Expression);// ExpressionTree.ExpressionToTree(item as Expression);
          }
          return null;
        }
      }
      // could not rewrite the value return default
      return null;
    }
  }
}

