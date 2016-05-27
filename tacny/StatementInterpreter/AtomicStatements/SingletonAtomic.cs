using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

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
            Contract.Assert(lv != null, Error.MkErr(st,8));
            Contract.Assert(tcce.OfSize(call_arguments, 3), Error.MkErr(st, 0, 3, call_arguments.Count));

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
                    solution_list.Add(new Solution(Copy()));
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
                if (formula.Data.GetType() == old_singleton.GetType() && formula.Modified == false)
                {
                    if (formula.Data is NameSegment)
                    {
                        curNs = (NameSegment)formula.Data;
                        oldNs = (NameSegment)old_singleton;

                    }
                    else if (formula.Data is UnaryOpExpr)
                    {
                        curNs = (NameSegment)((UnaryOpExpr)formula.Data).E;
                        oldNs = (NameSegment)((UnaryOpExpr)old_singleton).E;
                    }
                    else
                        Contract.Assert(false, Error.MkErr(formula.Data, -1));

                    if (curNs.Name == oldNs.Name)
                    {
                        ExpressionTree nt = formula.Copy();
                        nt.Data = new_term;

                        if (nt.Parent.LChild == nt)
                            nt.Parent.LChild = nt;
                        else
                            nt.Parent.RChild = nt;

                        nexp.Add(nt.Root.TreeToExpression());
                    }
                }
                return;
            }
            ReplaceTerm(old_singleton, new_term, formula.LChild, ref nexp);
            ReplaceTerm(old_singleton, new_term, formula.RChild, ref nexp);
        }

    }
}
