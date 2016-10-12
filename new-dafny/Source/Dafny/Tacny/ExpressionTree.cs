using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using System.Numerics;
using Microsoft.Boogie;
using ExistsExpr = Microsoft.Dafny.ExistsExpr;
using ForallExpr = Microsoft.Dafny.ForallExpr;
using Formal = Microsoft.Dafny.Formal;
using LiteralExpr = Microsoft.Dafny.LiteralExpr;
using QuantifierExpr = Microsoft.Dafny.QuantifierExpr;

namespace Tacny {
  /// <summary>
  /// Easy to manage tree representation of àn expression.
  /// </summary>
  public class ExpressionTree {
    public ExpressionTree Parent;
    public ExpressionTree LChild;
    public ExpressionTree RChild;
    public Expression Data;
    public bool Modified;
    public ExpressionTree Root;


    public ExpressionTree() { }

    public ExpressionTree(Expression data)
        : this(data, null) { }

    public ExpressionTree(Expression data, ExpressionTree parent)
        : this(data, parent, null, null) { }

    public ExpressionTree(Expression data, ExpressionTree parent, ExpressionTree lChild, ExpressionTree rChild)
        : this(data, parent, lChild, rChild, null) { }

    public ExpressionTree(Expression data, ExpressionTree parent, ExpressionTree lChild, ExpressionTree rChild,
        ExpressionTree root) {
      Data = data;
      Parent = parent;
      LChild = lChild;
      RChild = rChild;
      Root = root;
    }

    [Pure]
    public bool IsLeaf() {
      return LChild == null && RChild == null;
    }

    [Pure]
    public bool IsRoot() {
      return Parent == null;
    }

    [Pure]
    public bool IsLeftChild() {
      return Parent != null && Parent.LChild.Data.Equals(Data);
    }

    public bool IsRightChild() {
      return Parent != null && Parent.RChild.Data.Equals(Data);
    }

    public int OccurrenceOf(Expression exp) {
      if (!IsLeaf())
        return LChild.OccurrenceOf(exp) + (RChild?.OccurrenceOf(exp) ?? 0);

      if (exp.GetType() == Data.GetType()) {
        Console.WriteLine(exp.GetType());
        Console.WriteLine(Data.GetType());
        if (Data is NameSegment) {
          var ns1 = (NameSegment)Data;
          var ns2 = (NameSegment)exp;
          if (ns1.Name == ns2.Name)
            return 1;
        } else if (Data is LiteralExpr) {
          var ns1 = (LiteralExpr)Data;
          var ns2 = (LiteralExpr)exp;
          if (ns1.Value == ns2.Value)
            return 1;
        } else if (Data is UnaryOpExpr) {
          var ns1 = (UnaryOpExpr)Data;
          var ns2 = (UnaryOpExpr)exp;
          var e1 = (NameSegment)ns1.E;
          var e2 = (NameSegment)ns2.E;
          if (e1.Name == e2.Name)
            return 1;
        }
      }
      return 0;
    }


    private ExpressionTree _CopyTree() {
      var cl = new Cloner();
      if (IsLeaf())
        return new ExpressionTree(cl.CloneExpr(Data), null, null, null, Root);

      return new ExpressionTree(cl.CloneExpr(Data), null, LChild._CopyTree(), RChild?._CopyTree());
    }

    public ExpressionTree FindNode(ExpressionTree node) {
      Contract.Requires(node != null);
      if (Data.Equals(node.Data))
        return this;
      if (LChild != null) {
        var tmp = LChild.FindNode(node);
        if (tmp != null)
          return tmp;
      }
      if (RChild != null) {
        var tmp = RChild.FindNode(node);
        if (tmp != null)
          return tmp;
      }

      return null;
    }



    public static ExpressionTree FindAndReplaceNode(ExpressionTree tree, ExpressionTree newNode,
        ExpressionTree oldNode) {
      var node = oldNode; // tree.FindNode(oldNode);
      node.SetRoot();
      if (node.IsRoot()) {
        return newNode.CopyNode();
      }
      if (node.IsLeftChild()) {
        node.Parent.LChild = newNode;
      } else if (node.IsRightChild()) {
        node.Parent.RChild = newNode;
      } else {
        return tree;
      }

      return node.Root.Copy();
    }

    /// <summary>
    /// Method used to reasign parent references
    /// This method asumes that it's called from the root of the tree
    /// </summary>
    public void SetParent() {
      if (!IsLeaf()) {
        LChild.Parent = this;
        LChild.SetParent();
        if (RChild != null) {
          RChild.Parent = this;
          RChild.SetParent();
        }
      }
    }

    //public void FindAndResolveTacticApplication(Program tacnyProgram, Function fun) {
    //  if (IsLeaf()) {
    //    var aps = Data as ApplySuffix;
    //    if (aps == null)
    //      return;
    //    UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
    //    if (tacnyProgram.IsTacticCall(us)) {
    //      List<IVariable> variables = new List<IVariable>();
    //      ITactic tac = tacnyProgram.GetTactic(us);
    //      tacnyProgram.SetCurrent(tac, fun);
    //      variables.AddRange(fun.Formals);
    //      // get the resolved variables
    //      List<IVariable> resolved = new List<IVariable>();
    //      Console.Out.WriteLine($"Resolving {tac.Name} in {fun.Name}");
    //      resolved.AddRange(fun.Formals); // add input arguments as resolved variables
    //      var result = LazyTacny.Atomic.ResolveTactic(us, fun, tacnyProgram, variables, resolved);
    //      Data = result.State.DynamicContext.generatedExpressions[0];
    //      tacnyProgram.CurrentDebug.Fin();
    //      Modified = true;
    //    }
    //  } else {
    //    LChild.FindAndResolveTacticApplication(tacnyProgram, fun);
    //    RChild?.FindAndResolveTacticApplication(tacnyProgram, fun);
    //  }
    //}

    /**
 * Create a deep copy of the entire tree, and return the copy of the called node
 * 
 * */

    public ExpressionTree Copy() {
      var copy = this.Copy<ExpressionTree>();
      copy.SetRoot();
      copy.SetParent();
      return copy;
    }

    public ExpressionTree CopyNode() {

      var tree = _CopyTree();
      return tree;
    }

    public List<Expression> GetLeafData() {
      Contract.Ensures(Contract.Result<List<Expression>>() != null);
      List<Expression> leafs = new List<Expression>();

      if (IsLeaf())
        leafs.Add(Data);
      else {
        leafs.AddRange(LChild.GetLeafData());
        if (RChild != null)
          leafs.AddRange(RChild.GetLeafData());
      }

      return leafs;
    }

    public List<ExpressionTree> GetLeafs() {
      List<ExpressionTree> leafs = new List<ExpressionTree>();

      if (IsLeaf())
        leafs.Add(this);
      else {
        leafs.AddRange(LChild.GetLeafs());
        if (RChild != null)
          leafs.AddRange(RChild.GetLeafs());
      }

      return leafs;
    }

    public List<ExpressionTree> TreeToList() {
      if (Root == null)
        SetRoot();
      if (Parent == null)
        SetParent();
      List<ExpressionTree> nodes = new List<ExpressionTree> { this };

      if (LChild != null)
        nodes.AddRange(LChild.TreeToList());
      if (RChild != null)
        nodes.AddRange(RChild.TreeToList());

      return nodes;
    }

    public Expression TreeToExpression() {
      Contract.Ensures(Contract.Result<Expression>() != null);
      if (IsLeaf())
        return Data;
      if (Data is TacnyBinaryExpr) {
        TacnyBinaryExpr bexp = (TacnyBinaryExpr)Data;
        Expression E0 = LChild.TreeToExpression();
        Expression E1 = RChild?.TreeToExpression();
        return new TacnyBinaryExpr(bexp.tok, bexp.Op, E0, E1);
      }
      if (Data is BinaryExpr) {
        BinaryExpr bexp = (BinaryExpr)Data;
        Expression E0 = LChild.TreeToExpression();
        Expression E1 = RChild?.TreeToExpression();

        return new BinaryExpr(bexp.tok, bexp.Op, E0, E1);
      }
      if (Data is ChainingExpression) {
        List<Expression> operands = null;
        ChainingExpression ce = (ChainingExpression)Data;
        operands = GetLeafData();
        operands.RemoveAt(1); // hack to remove the duplicate name statement
        List<BinaryExpr.Opcode> operators = new List<BinaryExpr.Opcode>();
        BinaryExpr expr = (BinaryExpr)LChild.TreeToExpression();
        operators.Add(((BinaryExpr)expr.E0).Op);
        operators.Add(((BinaryExpr)expr.E1).Op);
        return new ChainingExpression(ce.tok, operands, operators, ce.PrefixLimits, expr);
      }
      if (Data is ParensExpression) {
        return new ParensExpression(Data.tok, LChild.TreeToExpression());
      }
      if (Data is QuantifierExpr) {
        QuantifierExpr qexp = (QuantifierExpr)Data;

        if (Data is ForallExpr)
          return new ForallExpr(qexp.tok, qexp.BoundVars, qexp.Range, LChild.TreeToExpression(),
              qexp.Attributes);
        if (Data is ExistsExpr)
          return new ExistsExpr(qexp.tok, qexp.BoundVars, qexp.Range, LChild.TreeToExpression(),
              qexp.Attributes);
      } else if (Data is NegationExpression) {
        return new NegationExpression(Data.tok, LChild.TreeToExpression());
      } else if (Data is SeqSelectExpr) {
        var e = (SeqSelectExpr)Data;
        return new SeqSelectExpr(e.tok, e.SelectOne, e.Seq, LChild.TreeToExpression(),
            RChild?.TreeToExpression());
      }

      return Data;
    }

    public void SetRoot() {
      Root = FindRoot();
      if (IsLeaf()) return;
      LChild.SetRoot();
      RChild?.SetRoot();
    }

    private ExpressionTree FindRoot() {
      return Parent == null ? this : Parent.FindRoot();
    }

    public static ExpressionTree ExpressionToTree(Expression exp) {
      var expt = new ExpressionTree().ExptToTree(exp);
      expt.SetRoot();
      return expt;
    }

    public ExpressionTree ExptToTree(Expression exp) {
      Contract.Requires(exp != null);
      Contract.Ensures(Contract.Result<ExpressionTree>() != null);
      ExpressionTree node;
      var binaryExpr = exp as TacnyBinaryExpr;
      if (binaryExpr != null) {
        var e = binaryExpr;
        node = new ExpressionTree(e) {
          LChild = ExpressionToTree(e.E0),
          RChild = ExpressionToTree(e.E1)
        };
      } else if (exp is BinaryExpr) {
        var e = (BinaryExpr)exp;
        node = new ExpressionTree(e) {
          LChild = ExpressionToTree(e.E0),
          RChild = ExpressionToTree(e.E1)
        };

      } else if (exp is QuantifierExpr) {
        var e = (QuantifierExpr)exp;
        node = new ExpressionTree(e) { LChild = ExpressionToTree(e.Term) };
      } else if (exp is ParensExpression) {
        var e = (ParensExpression)exp;
        node = new ExpressionTree(e) { LChild = ExpressionToTree(e.E) };
      } else if (exp is ChainingExpression) {
        var e = (ChainingExpression)exp;
        node = new ExpressionTree(e) { LChild = ExpressionToTree(e.E) };
      } else if (exp is NegationExpression) {
        var e = (NegationExpression)exp;
        node = new ExpressionTree(e) { LChild = ExpressionToTree(e.E) };
      } else if (exp is SeqSelectExpr) {
        var e = (SeqSelectExpr)exp;
        node = new ExpressionTree(e) { LChild = ExpressionToTree(e.E0) };
        if (e.E1 != null) {
          node.RChild = ExpressionToTree(e.E1);
        }
      } else {
        node = new ExpressionTree(exp);
      }

      if (node.LChild != null)
        node.LChild.Parent = node;
      if (node.RChild != null)
        node.RChild.Parent = node;
      return node;
    }


    protected Expression EvaluateExpression(ExpressionTree expt, ProofState state) {
      Contract.Requires(expt != null);
      if (expt.IsLeaf()) {
        return EvaluateLeaf(expt, state) as LiteralExpr;
      }
      var bexp = (BinaryExpr)expt.Data;
      if (BinaryExpr.IsEqualityOp(bexp.Op)) {
        bool boolVal = EvaluateEqualityExpression(expt, state);
        return new LiteralExpr(new Token(), boolVal);
      }
      var lhs = EvaluateExpression(expt.LChild, state) as LiteralExpr;
      var rhs = EvaluateExpression(expt.RChild, state) as LiteralExpr;
      // for now asume lhs and rhs are integers
      var l = (BigInteger)lhs?.Value;
      var r = (BigInteger)rhs?.Value;

      BigInteger res = 0;


      switch (bexp.Op) {
        case BinaryExpr.Opcode.Sub:
          res = BigInteger.Subtract(l, r);
          break;
        case BinaryExpr.Opcode.Add:
          res = BigInteger.Add(l, r);
          break;
        case BinaryExpr.Opcode.Mul:
          res = BigInteger.Multiply(l, r);
          break;
        case BinaryExpr.Opcode.Div:
          res = BigInteger.Divide(l, r);
          break;
      }

      return new LiteralExpr(lhs.tok, res);
    }

    public static bool EvaluateEqualityExpression(ExpressionTree expt, ProofState state) {
      Contract.Requires(expt != null);
      // if the node is leaf, cast it to bool and return
      if (expt.IsLeaf()) {
        var lit = EvaluateLeaf(expt, state) as LiteralExpr;
        return lit?.Value is bool && (bool)lit.Value;
      }
      // left branch only
      if (expt.LChild != null && expt.RChild == null)
        return EvaluateEqualityExpression(expt.LChild, state);
      // if there is no more nesting resolve the expression
      if (expt.LChild.IsLeaf() && expt.RChild.IsLeaf()) {
        LiteralExpr lhs = null;
        LiteralExpr rhs = null;
        lhs = EvaluateLeaf(expt.LChild, state).FirstOrDefault() as LiteralExpr;
        rhs = EvaluateLeaf(expt.RChild, state).FirstOrDefault() as LiteralExpr;
        Contract.Assert(lhs != null && rhs != null);
        var bexp = expt.Data as BinaryExpr;
        int res = -1;
        if (lhs?.Value is BigInteger) {
          var l = (BigInteger)lhs.Value;
          var r = (BigInteger)rhs?.Value;
          res = l.CompareTo(r);
        } else if (lhs?.Value is string) {
          var l = (string)lhs.Value;
          var r = rhs?.Value as string;
          res = string.Compare(l, r, StringComparison.Ordinal);
        } else if (lhs?.Value is bool) {
          res = ((bool)lhs.Value).CompareTo(rhs?.Value != null && (bool)rhs?.Value);
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
      } else {
        // evaluate a nested expression

        var bexp = expt.Data as BinaryExpr;
        switch (bexp.Op) {
          case BinaryExpr.Opcode.And:
            return EvaluateEqualityExpression(expt.LChild, state) &&
                   EvaluateEqualityExpression(expt.RChild, state);
          case BinaryExpr.Opcode.Or:
            return EvaluateEqualityExpression(expt.LChild, state) ||
                   EvaluateEqualityExpression(expt.RChild, state);
        }
      }
      return false;
    }

    public static IEnumerable<object> EvaluateLeaf(ExpressionTree expt, ProofState state) {
      Contract.Requires(expt != null && expt.IsLeaf());
      foreach (var item in Interpreter.EvalTacnyExpression(state, expt.Data))
        yield return item;
    }

    public static bool IsResolvable(ExpressionTree expt, ProofState state) {
      Contract.Requires(expt.IsRoot());
      var leafs = expt.GetLeafData();
      if (leafs == null) throw new ArgumentNullException(nameof(leafs));

      foreach (var leaf in leafs) {
        if (leaf is NameSegment) {
          var ns = leaf as NameSegment;
          if (state.ContainTacnyVal(ns)) {
            var local = state.GetTacnyVarValue(ns);
            if (local is ApplySuffix || local is IVariable || local is NameSegment || local is Statement)
              return false;
          } else {
            return false;
          }
        } else if (!(leaf is LiteralExpr)) {
          if (leaf is ApplySuffix) {
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Resolve all variables in expression to either literal values
    /// or to orignal declared nameSegments
    /// </summary>
    /// <param name="guard"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static IEnumerable<ExpressionTree> ResolveExpression(ExpressionTree guard, ProofState state) {
      Contract.Requires<ArgumentNullException>(guard != null, "guard");
      Contract.Requires<ArgumentNullException>(state != null, "state");

      if (guard.IsLeaf()) {
        if (!(guard.Data is NameSegment)) {
          yield return guard.Copy();
        }
        var newGuard = guard.Copy();
        var result = EvaluateLeaf(newGuard, state);
        foreach (var item in result) {
          Contract.Assert(result != null);
          Expression newNs = null; // potential encapsulation problems
          if (item is MemberDecl) {
            var md = item as MemberDecl;
            newNs = new StringLiteralExpr(new Token(), md.Name, true);
          } else if (item is Formal) {
            var tmp = item as Formal;
            newNs = new NameSegment(tmp.tok, tmp.Name, null);
          } else if (item is NameSegment) {
            newNs = item as NameSegment;
          } else {
            newNs = item as Expression; // Dafny.LiteralExpr;
          }
          newGuard.Data = newNs;
          yield return newGuard;
        }
      } else {
        foreach (var lChild in ResolveExpression(guard.LChild, state)) {
          if (guard.RChild != null) {
            foreach (var rChild in ResolveExpression(guard.RChild, state)) {
              yield return new ExpressionTree(guard.Data, null, lChild, rChild);
            }
          } else {
            yield return new ExpressionTree(guard.Data, null, lChild, null);
          }
        }
      }
    }
  }
}