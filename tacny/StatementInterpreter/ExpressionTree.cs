using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;


namespace Tacny
{
    /// <summary>
    /// Easy to manage tree representation of àn expression.
    /// </summary>
    public class ExpressionTree
    {
        public ExpressionTree parent;
        public ExpressionTree lChild;
        public ExpressionTree rChild;
        public Expression data;
        public bool was_replaced = false;
        public ExpressionTree root;

        public ExpressionTree(Expression data)
            : this(data, null)
        { }

        public ExpressionTree(Expression data, ExpressionTree parent)
            : this(data, parent, null, null)
        { }

        public ExpressionTree(Expression data, ExpressionTree parent, ExpressionTree lChild, ExpressionTree rChild)
            : this(data, parent, lChild, rChild, null)
        { }

        public ExpressionTree(Expression data, ExpressionTree parent, ExpressionTree lChild, ExpressionTree rChild, ExpressionTree root)
        {
            this.data = data;
            this.parent = parent;
            this.lChild = lChild;
            this.rChild = rChild;
            this.root = root;
        }
        [Pure]
        public bool IsLeaf()
        {
            return lChild == null && rChild == null;
        }
        [Pure]
        public bool isRoot()
        {
            return parent == null;
        }

        public int OccurrenceOf(Expression exp)
        {
            if (!this.IsLeaf())
                return lChild.OccurrenceOf(exp) + (rChild == null ? 0 : rChild.OccurrenceOf(exp));

            if (exp.GetType() == data.GetType())
            {
                Console.WriteLine(exp.GetType());
                Console.WriteLine(data.GetType());
                if (data is NameSegment)
                {
                    var ns1 = (NameSegment)data;
                    var ns2 = (NameSegment)exp;
                    if (ns1.Name == ns2.Name)
                        return 1;
                }
                else if (data is Dafny.LiteralExpr)
                {
                    var ns1 = (Dafny.LiteralExpr)data;
                    var ns2 = (Dafny.LiteralExpr)exp;
                    if (ns1.Value == ns2.Value)
                        return 1;
                }
                else if (data is UnaryOpExpr)
                {
                    var ns1 = (UnaryOpExpr)data;
                    var ns2 = (UnaryOpExpr)exp;
                    var e1 = (NameSegment)ns1.E;
                    var e2 = (NameSegment)ns2.E;
                    if (e1.Name == e2.Name)
                        return 1;
                }
            }
            return 0;
        }


        private ExpressionTree _CopyTree()
        {
            if (IsLeaf())
                return new ExpressionTree(data, null, null, null);

            return new ExpressionTree(data, null, lChild._CopyTree(), (rChild == null ? null : rChild._CopyTree()));
        }

        public ExpressionTree FindNode(ExpressionTree node)
        {
            Contract.Requires(node != null);
            Contract.Ensures(Contract.Result<ExpressionTree>() != null);
            if (data.Equals(node.data))
                return this;
            if (lChild != null)
            {
                var tmp = lChild.FindNode(node);
                if (tmp != null)
                    return tmp;
            }
            if (rChild != null)
            {
                var tmp = rChild.FindNode(node);
                if (tmp != null)
                    return tmp;
            }

            return null;
        }

        /*
         * Method used to reasign parent references
         * This method asumes that it's called from the root of the tree
         */
        private void SetParent()
        {
            if (!IsLeaf())
            {
                lChild.parent = this;
                lChild.SetParent();
                if (rChild != null)
                {
                    rChild.parent = this;
                    rChild.SetParent();
                }
            }

        }

        public void FindAndResolveTacticApplication(Program tacnyProgram, Function fun)
        {
            if(IsLeaf())
            {
                var aps = data as ApplySuffix;
                if (aps == null)
                    return;
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                if(tacnyProgram.IsTacticCall(us))
               { 
                    List<IVariable> variables = new List<IVariable>();
                    ITactic tac = tacnyProgram.GetTactic(us);
                    tacnyProgram.SetCurrent(tac, fun);
                    variables.AddRange(fun.Formals);
                    // get the resolved variables
                    List<IVariable> resolved = new List<IVariable>();
                    Console.Out.WriteLine(string.Format("Resolving {0} in {1}", tac.Name, fun.Name));
                    resolved.AddRange(fun.Formals); // add input arguments as resolved variables
                    Expression exp = LazyTacny.Atomic.ResolveTacticFunction(tac, us, fun, tacnyProgram, variables, resolved);
                    tacnyProgram.currentDebug.Fin();
                    data = exp;
                }
            } else
            {
                lChild.FindAndResolveTacticApplication(tacnyProgram, fun);
                if(rChild != null)
                    rChild.FindAndResolveTacticApplication(tacnyProgram, fun);
            }
        }

        /**
         * Create a deep copy of the entire tree, and return the copy of the called node
         * 
         * */
        public ExpressionTree Copy()
        {
            Contract.Ensures(Contract.Result<ExpressionTree>() != null);
            if (root == null)
                this.SetRoot();
            ExpressionTree et = root._CopyTree();
            et.SetParent();
            et.SetRoot();
            var tmp = et.FindNode(this);
            Contract.Assert(tmp != null);
            return tmp;
        }

        public List<Expression> GetLeafs()
        {
            Contract.Ensures(Contract.Result<List<Expression>>() != null);
            List<Expression> leafs = new List<Expression>();

            if (IsLeaf())
                leafs.Add(data);
            else
            {
                leafs.AddRange(lChild.GetLeafs());
                if (rChild != null)
                    leafs.AddRange(rChild.GetLeafs());
            }

            return leafs;
        }

        public Expression TreeToExpression()
        {
            Contract.Ensures(Contract.Result<Expression>() != null);
            if (IsLeaf())
                return data;
            else
            {
                if (data is BinaryExpr)
                {
                    BinaryExpr bexp = (BinaryExpr)data;
                    Expression E0 = lChild.TreeToExpression();
                    Expression E1 = (rChild == null ? null : rChild.TreeToExpression());

                    return new BinaryExpr(bexp.tok, bexp.Op, E0, E1);
                }
                else if (data is ChainingExpression)
                {
                    List<Expression> operands = null;
                    ChainingExpression ce = (ChainingExpression)data;
                    operands = this.GetLeafs();
                    operands.RemoveAt(1); // hack to remove the duplicate name statement
                    List<BinaryExpr.Opcode> operators = new List<BinaryExpr.Opcode>();
                    BinaryExpr expr = (BinaryExpr)lChild.TreeToExpression();
                    operators.Add(((BinaryExpr)expr.E0).Op);
                    operators.Add(((BinaryExpr)expr.E1).Op);
                    return new ChainingExpression(ce.tok, operands, operators, ce.PrefixLimits, expr);
                }
                else if (data is ParensExpression)
                {
                    return new ParensExpression(data.tok, lChild.TreeToExpression());
                }
                else if (data is Dafny.QuantifierExpr)
                {
                    Dafny.QuantifierExpr qexp = (Dafny.QuantifierExpr)data;

                    if (data is Dafny.ForallExpr)
                        return new Dafny.ForallExpr(qexp.tok, qexp.BoundVars, qexp.Range, lChild.TreeToExpression(), qexp.Attributes);
                    else if (data is Dafny.ExistsExpr)
                        return new Dafny.ExistsExpr(qexp.tok, qexp.BoundVars, qexp.Range, lChild.TreeToExpression(), qexp.Attributes);

                }
                else if (data is NegationExpression)
                {
                    return new NegationExpression(data.tok, lChild.TreeToExpression());
                }
                return data;
            }
        }

        public void SetRoot()
        {
            root = FindRoot();
            if (!IsLeaf())
            {
                lChild.SetRoot();
                if (rChild != null)
                    rChild.SetRoot();
            }
        }

        private ExpressionTree FindRoot()
        {
            if (parent == null)
                return this;
            return parent.FindRoot();
        }

        public static ExpressionTree ExpressionToTree(Expression exp)
        {
            Contract.Requires(exp != null);
            Contract.Ensures(Contract.Result<ExpressionTree>() != null);
            ExpressionTree node;
            if (exp is BinaryExpr)
            {
                var e = (BinaryExpr)exp;
                node = new ExpressionTree(e);
                node.lChild = ExpressionToTree(e.E0);
                node.rChild = ExpressionToTree(e.E1);
                node.lChild.parent = node;
                node.rChild.parent = node;
            }
            else if (exp is Dafny.QuantifierExpr)
            {
                var e = (Dafny.QuantifierExpr)exp;
                node = new ExpressionTree(e);
                node.lChild = ExpressionToTree(e.Term);
                node.lChild.parent = node;
            }
            else if (exp is ParensExpression)
            {
                var e = (ParensExpression)exp;
                node = new ExpressionTree(e);
                node.lChild = ExpressionToTree(e.E);
                node.lChild.parent = node;
            }
            else if (exp is ChainingExpression)
            {
                var e = (ChainingExpression)exp;
                node = new ExpressionTree(e);
                node.lChild = ExpressionToTree(e.E);
                node.lChild.parent = node;
            }
            else if (exp is NegationExpression)
            {
                var e = (NegationExpression)exp;
                node = new ExpressionTree(e);
                node.lChild = ExpressionToTree(e.E);
                node.lChild.parent = node;
            }
            else // if (exp is NameSegment || exp is Dafny.LiteralExpr)
            {
                node = new ExpressionTree(exp);
            }
            return node;
        }
    }
}
