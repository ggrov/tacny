using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {

  public class ReplaceConstantAtomic : Atomic, IAtomicLazyStmt {


    public ReplaceConstantAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return ReplaceConstant(st);
    }


    private IEnumerable<Solution> ReplaceConstant(Statement st) {
      IVariable lv = null;
      List<Expression> callArgs;
      InitArgs(st, out lv, out callArgs);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(callArgs.Count == 3, Error.MkErr(st, 0, 3, callArgs.Count));
      var varLists = new List<List<IVariable>>();
      var constLists = new List<List<Expression>>();

      foreach (var arg2 in ResolveExpression(callArgs[1])) {
        var tmp = arg2 as List<Expression>;
        Contract.Assert(tmp != null, Error.MkErr(st, 1, "Term Seq"));
        constLists.Add(tmp);
      }
      foreach (var arg2 in ResolveExpression(callArgs[2])) {
        var tmp = arg2 as List<IVariable>;
        Contract.Assert(tmp != null, Error.MkErr(st, 1, "Term Seq"));
        varLists.Add(tmp);
      }

      foreach (var arg1 in ResolveExpression(callArgs[0])) {
        var expression = arg1 as Expression;
        Contract.Assert(expression != null, Error.MkErr(st, 1, "Term"));
        foreach (var varList in varLists) {
          foreach (var constList in constLists) {
            foreach (var item in ReplaceConstants(ExpressionTree.ExpressionToTree(expression), constList, varList)) {
              yield return AddNewLocal(lv, item);
            }
          }
        }
      }
    }


    private IEnumerable<Expression> ReplaceConstants(ExpressionTree expression, List<Expression> constants, List<IVariable> vars) {
      if (expression == null)
        yield break;
      if (expression.Root == null)
        expression.SetRoot();

      if (expression.IsLeaf()) {
        if (HasConstant(expression.Data, constants)) {
          foreach (var var in vars) {
            if (!ValidateType(var, expression.Parent.TreeToExpression() as BinaryExpr))
              continue;
            var newVal = ExpressionTree.ExpressionToTree(VariableToExpression(var));
            var copy = expression.Root.Copy();
            var newTree = ExpressionTree.FindAndReplaceNode(copy, newVal, expression.Copy());
            yield return newTree.Root.TreeToExpression();
            foreach (var item in ReplaceConstants(newTree.Root, constants, vars))
              yield return item;
          }
        }
      } else {
        foreach (var item in ReplaceConstants(expression.LChild, constants, vars))
          yield return item;
        foreach (var item in ReplaceConstants(expression.RChild, constants, vars))
          yield return item;
      }

    }

    private bool HasConstant(Expression constant, List<Expression> constants) {

      if (!(constant is LiteralExpr || constant is ExprDotName))
        return false;

      return constants.Exists(j => (j as LiteralExpr)?.Value.ToString() == (constant as LiteralExpr)?.Value.ToString() || SingletonEquality(j, constant as ExprDotName));
    }
  }
}