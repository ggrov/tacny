using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class SingletonAtomic : Atomic, IAtomicStmt
    {

        public SingletonAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Replace(st, ref solution_list);
        }

        /// <summary>
        /// Replace a singleton with a new term
        /// </summary>
        /// <param name="st">replace_singleton(); Statement</param>
        /// <param name="solution_list">Reference to the solution tree</param>
        /// <returns> null if success; error message otherwise</returns>
        private void Replace(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            List<Expression> processed_args = new List<Expression>(3);
            Expression old_singleton = null;
            Expression new_term = null;
            Expression formula = null;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st,8));
            Contract.Assert(tcce.OfSize(call_arguments, 3), Util.Error.MkErr(st, 0, 3, call_arguments.Count));

            ProcessArg(call_arguments[0], out old_singleton);
            ProcessArg(call_arguments[1], out new_term);
            ProcessArg(call_arguments[2], out formula);
            
            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);

            List<Expression> exp_list = new List<Expression>();

            ReplaceTerm(old_singleton, new_term, et, ref exp_list);
            // branch
            if (exp_list.Count > 0)
            {
                for (int i = 0; i < exp_list.Count; i++)
                {
                    AddLocal(lv, exp_list[i]);
                    solution_list.Add(new Solution(this.Copy()));
                }
            }
        }


        private void ReplaceTerm(Expression old_singleton, Expression new_term, ExpressionTree formula, ref List<Expression> nexp)
        {
            Contract.Requires(nexp != null);
            Contract.Requires(old_singleton != null);
            Contract.Requires(new_term != null);
            NameSegment curNs = null;
            NameSegment oldNs = null;

            if (formula == null)
                return;

            if (formula.IsLeaf())
            {
                if (formula.data.GetType() == old_singleton.GetType() && formula.was_replaced == false)
                {
                    if (formula.data is NameSegment)
                    {
                        curNs = (NameSegment)formula.data;
                        oldNs = (NameSegment)old_singleton;

                    }
                    else if (formula.data is UnaryOpExpr)
                    {
                        curNs = (NameSegment)((UnaryOpExpr)formula.data).E;
                        oldNs = (NameSegment)((UnaryOpExpr)old_singleton).E;
                    }
                    else
                        Contract.Assert(false, Util.Error.MkErr(formula.data, -1));

                    if (curNs.Name == oldNs.Name)
                    {
                        ExpressionTree nt = formula.Copy();
                        nt.data = new_term;

                        if (nt.parent.lChild == nt)
                            nt.parent.lChild = nt;
                        else
                            nt.parent.rChild = nt;

                        nexp.Add(nt.root.TreeToExpression());
                    }
                }
                return;
            }
            ReplaceTerm(old_singleton, new_term, formula.lChild, ref nexp);
            ReplaceTerm(old_singleton, new_term, formula.rChild, ref nexp);
        }

    }
}
