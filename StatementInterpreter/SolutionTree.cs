using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using Tacny;

namespace Tacny
{
    public class SolutionTree
    {
        int id;
        private Action _state;
        public Action state
        {
            get { return _state; }
            set
            {
                if (_state == null)
                    _state = value;
            }
        }
        public readonly ErrorInformation errorInfo;
        public List<SolutionTree> children;                     // list of children
        private SolutionTree _parent;                               // parent node
        public SolutionTree parent
        {
            get { return _parent; }
            set
            {
                if (_parent == null)
                    _parent = value;
            }
        }
        private SolutionTree _root;
        public SolutionTree root
        {
            get { return _root; }
            set
            {
                if (_root == null)
                    _root = value;
            }
        }
        public bool isFinal = false;                         // indicates whether the node is fully resolved tactic
        public Statement last_resolved;                         // last satement resolved in the tactic body


        public SolutionTree(Action state)
            : this(state, null)
        { }

        public SolutionTree(Action state, SolutionTree parent)
            : this(state, parent, null)
        { }

        public SolutionTree(Action state, SolutionTree parent, Statement last_resolved)
        {
            if (parent == null)
                this.id = 0;
            else
                this.id = parent.id + 1;
            this.state = state;
            this.parent = parent;
            this.last_resolved = last_resolved;
            SetRoot();
        }

        public void SetRoot()
        {
            if (parent == null)
                root = this;
            root = FindRoot();
        }

        public SolutionTree FindRoot()
        {
            if (parent == null)
                return this;
            return parent.FindRoot();
        }

        public void AddChild(SolutionTree node)
        {
            if (children == null)
                children = new List<SolutionTree>();

            children.Add(node);
        }

        public bool RemoveChild(SolutionTree node)
        {
            Contract.Requires(children != null);
            return children.Remove(node);
        }

        public bool isLeaf()
        {
            return children == null || children.Count == 0;
        }

        public SolutionTree GetLeftMost()
        {
            if (isLeaf())
                return this;
            return children[0].GetLeftMost();
        }

        public SolutionTree GetLeftMostUndersolved()
        {
            if (isLeaf() && !isFinal)
                return this;
            else
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (!children[i].isFinal)
                        return children[i];
                }
            }

            return null;
        }

        public void PrintTree(string separator, int counter = 0)
        {
            for (int i = 0; i < counter; i++)
            {
                Console.Write(separator);     
            }
            Console.WriteLine(id);
            if (children != null)
            {
                foreach (SolutionTree st in children)
                {
                    st.PrintTree(separator, counter+1);
                }
            }
        }

        public string GenerateProgram(ref Dafny.Program prog)
        {
            if (!isLeaf())
                throw new Exception("Only leaf nodes can be generated to programs");

            List<Dafny.Program> prog_list = new List<Dafny.Program>();
            Action ac = this.state;
            Method m = (Method)ac.md;
            UpdateStmt tac_call = ac.tac_call;
            List<Statement> body = m.Body.Body;
            body = InsertSolution(body, tac_call, ac.resolved);

            m = new Method(m.tok, m.Name, m.HasStaticKeyword, m.IsGhost,
                m.TypeArgs, m.Ins, m.Outs, m.Req, m.Mod, m.Ens, m.Decreases,
                new BlockStmt(m.Body.Tok, m.Body.EndTok, body), m.Attributes, m.SignatureEllipsis);
            ClassDecl curDecl;

            for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++)
            {
                TopLevelDecl d = prog.DefaultModuleDef.TopLevelDecls[i];
                if (d is ClassDecl)
                {
                    // scan each member for tactic calls and resolve if found
                    curDecl = (ClassDecl)d;
                    for (int j = 0; j < curDecl.Members.Count; j++)
                    {
                        MemberDecl md = curDecl.Members[j];
                        if (md is Method)
                        {
                            Method old_m = (Method)md;
                            if (old_m.Name == m.Name)
                            {
                                curDecl.Members[j] = m;
                            }
                        }
                    }
                    prog.DefaultModuleDef.TopLevelDecls[i] = RemoveTactics(curDecl);
                }
            }

            return null;
        }

        private static List<Statement> InsertSolution(List<Statement> body, UpdateStmt tac_call, List<Statement> solution)
        {
            WhileStmt ws = null;
            BlockStmt bs = null;
            int index = body.IndexOf(tac_call);
            List<Statement> newBody = new List<Statement>();
            if (index == 0)
            {
                newBody = body;
                newBody.InsertRange(0, solution);
                return newBody;
            }

            // check where from tac_call has been made
            int i = index + 1;
            while (i < body.Count)
            {
                Statement stmt = body[i];
                // if we found a block statement check behind to find the asociated while statement
                if (stmt is BlockStmt)
                {
                    int j = index;
                    while (j > 0)
                    {
                        Statement stmt_2 = body[j];
                        if (stmt_2 is WhileStmt)
                        {
                            ws = (WhileStmt)stmt_2;
                            break;
                        }
                        else if (!(stmt_2 is UpdateStmt))
                        {
                            return null;
                        }

                        j--;
                    }
                    bs = (BlockStmt)stmt;
                    break;
                }
                else if (!(stmt is UpdateStmt))
                {
                    return null;
                }

                i++;
            }
            //// tactic called from a while statement
            if (ws != null && bs != null)
            {
                Statement[] tmp = body.ToArray();
                int l_bound = body.IndexOf(ws);
                int u_boud = body.IndexOf(bs);
                // tactic called in a while statement should 
                //  return onlyt a single solution item which is a WhileStmt
                WhileStmt mod_ws = (WhileStmt)solution[0];
                mod_ws = new WhileStmt(mod_ws.Tok, mod_ws.EndTok, mod_ws.Guard, mod_ws.Invariants,
                    mod_ws.Decreases, mod_ws.Mod, bs);
                tmp[l_bound] = mod_ws;
                l_bound++;


                // for now remove everything between while stmt and block stmt
                while (l_bound <= u_boud)
                {
                    tmp[l_bound] = null;
                    l_bound++;
                }

                foreach (Statement st in tmp)
                {
                    if (st != null)
                        newBody.Add(st);
                }
            }
            else
            {
                newBody = body;
                newBody.InsertRange(index, solution);
            }

            return newBody;
        }

        private static TopLevelDecl RemoveTactics(ClassDecl cd)
        {
            List<MemberDecl> mdl = new List<MemberDecl>();
            foreach (MemberDecl md in cd.Members)
            {
                if (!(md is Tactic))
                    mdl.Add(md);

            }
            return new ClassDecl(cd.tok, cd.Name, cd.Module, cd.TypeArgs, mdl, cd.Attributes, cd.TraitsTyp);
        }


    }


    /// <summary>
    /// Easy to manage tree representation of the expression.
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

        public bool isLeaf()
        {
            return lChild == null && rChild == null;
        }

        public int OccurrenceOf(Expression exp)
        {
            if (!this.isLeaf())
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
            if (isLeaf())
                return new ExpressionTree(data, null, null, null);

            return new ExpressionTree(data, null, lChild._CopyTree(), (rChild == null ? null : rChild._CopyTree()));
        }

        public ExpressionTree FindNode(ExpressionTree node)
        {
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
            if (!isLeaf())
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

        /**
         * Create a deep copy of the entire tree, and return the copy of the called node
         * 
         * */
        public ExpressionTree Copy()
        {
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
            List<Expression> leafs = new List<Expression>();

            if (isLeaf())
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
            if (isLeaf())
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
            if (!isLeaf())
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
