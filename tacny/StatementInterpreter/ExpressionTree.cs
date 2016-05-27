using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

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

    public ExpressionTree(Expression data, ExpressionTree parent, ExpressionTree lChild, ExpressionTree rChild, ExpressionTree root) {
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
      if (IsLeaf())
        return new ExpressionTree(Data, null, null, null, Root);

      return new ExpressionTree(Data, null, LChild._CopyTree(), RChild?._CopyTree());
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



    public static ExpressionTree FindAndReplaceNode(ExpressionTree tree, ExpressionTree newNode, ExpressionTree oldNode) {
      var node = oldNode;// tree.FindNode(oldNode);
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

    /*
     * Method used to reasign parent references
     * This method asumes that it's called from the root of the tree
     */
    private void SetParent() {
      if (!IsLeaf()) {
        LChild.Parent = this;
        LChild.SetParent();
        if (RChild != null) {
          RChild.Parent = this;
          RChild.SetParent();
        }
      }

    }

    public void FindAndResolveTacticApplication(Program tacnyProgram, Function fun) {
      if (IsLeaf()) {
        var aps = Data as ApplySuffix;
        if (aps == null)
          return;
        UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
        if (tacnyProgram.IsTacticCall(us)) {
          List<IVariable> variables = new List<IVariable>();
          ITactic tac = tacnyProgram.GetTactic(us);
          tacnyProgram.SetCurrent(tac, fun);
          variables.AddRange(fun.Formals);
          // get the resolved variables
          List<IVariable> resolved = new List<IVariable>();
          Console.Out.WriteLine($"Resolving {tac.Name} in {fun.Name}");
          resolved.AddRange(fun.Formals); // add input arguments as resolved variables
          var result = LazyTacny.Atomic.ResolveTactic(us, fun, tacnyProgram, variables, resolved);
          Data = result.State.DynamicContext.generatedExpressions[0];
          tacnyProgram.CurrentDebug.Fin();
          Modified = true;
        }
      } else {
        LChild.FindAndResolveTacticApplication(tacnyProgram, fun);
        RChild?.FindAndResolveTacticApplication(tacnyProgram, fun);
      }
    }

    /**
     * Create a deep copy of the entire tree, and return the copy of the called node
     * 
     * */
    public ExpressionTree Copy() {
      Contract.Ensures(Contract.Result<ExpressionTree>() != null);
      if (Root == null)
        SetRoot();
      var et = Root?._CopyTree();
      et.SetParent();
      et.SetRoot();
      return !et.IsLeaf() ? et.FindNode(this) : et;
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
          return new ForallExpr(qexp.tok, qexp.BoundVars, qexp.Range, LChild.TreeToExpression(), qexp.Attributes);
        if (Data is ExistsExpr)
          return new ExistsExpr(qexp.tok, qexp.BoundVars, qexp.Range, LChild.TreeToExpression(), qexp.Attributes);
      } else if (Data is NegationExpression) {
        return new NegationExpression(Data.tok, LChild.TreeToExpression());
      } else if (Data is SeqSelectExpr) {
        var e = (SeqSelectExpr)Data;
        return new SeqSelectExpr(e.tok, e.SelectOne, e.Seq, LChild.TreeToExpression(), RChild?.TreeToExpression());
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
  }
}
