using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class SingletonAtomic : Atomic, IAtomicStmt
    {

        public override string FormatError(string error)
        {
            return "ERROR replace_singleton: " + error;
        }

        public SingletonAtomic(Atomic atomic)
            : base(atomic)
        { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Replace(st, ref solution_list);
        }


        /// <summary>
        /// Replace a singleton with a new term
        /// </summary>
        /// <param name="st">replace_singleton(); Statement</param>
        /// <param name="solution_list">Reference to the solution tree</param>
        /// <returns> null if success; error message otherwise</returns>
        public string Replace(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            List<Expression> processed_args = new List<Expression>(3);
            Expression old_singleton = null;
            Expression new_term = null;
            Expression formula = null;
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return FormatError(err);

            if (call_arguments.Count != 3)
                return FormatError("Wrong number of method arguments; Expected 3 got " + call_arguments.Count);

            err = ProcessArg(call_arguments[0], out old_singleton);
            if (err != null)
                return FormatError(err);

            err = ProcessArg(call_arguments[1], out new_term);
            if (err != null)
                return FormatError(err);

            err = ProcessArg(call_arguments[2], out formula);
            if (err != null)
                return FormatError(err);

            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);

            List<Expression> exp_list = new List<Expression>();

            err = ReplaceTerm(old_singleton, new_term, et, ref exp_list);
            if (err != null)
                return err;
            // branch
            if (exp_list.Count > 0)
            {
                IncTotalBranchCount(exp_list.Count);
                for (int i = 0; i < exp_list.Count; i++)
                {
                    AddLocal(lv, exp_list[i]);
                    solution_list.Add(new Solution(this.Copy()));
                }
            }
            return null;
        }


        private string ReplaceTerm(Expression old_singleton, Expression new_term, ExpressionTree formula, ref List<Expression> nexp)
        {
            Contract.Requires(nexp != null);
            Contract.Requires(old_singleton != null);
            Contract.Requires(new_term != null);
            NameSegment curNs = null;
            NameSegment oldNs = null;

            if (formula == null)
                return null;

            if (formula.isLeaf())
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
                        return "Unsuported data: " + formula.data.GetType();

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
                return null;
            }
            ReplaceTerm(old_singleton, new_term, formula.lChild, ref nexp);
            ReplaceTerm(old_singleton, new_term, formula.rChild, ref nexp);
            return null;
        }

    }
}
