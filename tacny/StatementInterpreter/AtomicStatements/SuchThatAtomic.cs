using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Util;
namespace Tacny
{
    class SuchThatAtomic : Atomic, IAtomicStmt
    {
        public SuchThatAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            SuchThat(st, ref solution_list);
        }

        private void SuchThat(Statement st, ref List<Solution> solution_list)
        {
            object value = null;
            dynamic dynamic_val = null;
            TacticVarDeclStmt tvds = st as TacticVarDeclStmt;
            Contract.Assert(tvds != null, Util.Error.MkErr(st, 5, typeof(TacticVarDeclStmt), st.GetType()));

            AssignSuchThatStmt suchThat = tvds.Update as AssignSuchThatStmt;
            Contract.Assert(suchThat != null, Util.Error.MkErr(st, 5, typeof(AssignSuchThatStmt), tvds.Update.GetType()));
            
            BinaryExpr bexp = suchThat.Expr as BinaryExpr;
            Contract.Assert(bexp != null, Util.Error.MkErr(st, 5, typeof(BinaryExpr), suchThat.Expr.GetType()));

            Expression lhs = bexp.E0;
            Expression rhs = bexp.E1;

            // big bad hack
            if (lhs is BinaryExpr && rhs is BinaryExpr)
            {
                ResolveLhs(lhs as BinaryExpr, tvds.Locals[0], out value);
                Contract.Assert(value != null);
                BinaryExpr rrhs = rhs as BinaryExpr;

                Expression e0 = rrhs.E0;
                Expression e1 = rrhs.E1;
                if (!(e0 is NameSegment) || !(e1 is NameSegment))
                    Contract.Assert(false, String.Format("Currently nested binary expressions are not supported"));

                IVariable form = localContext.GetLocalValueByName(e0 as NameSegment) as IVariable;
                Contract.Assert(form != null, Util.Error.MkErr(e0, 6, (e0 as NameSegment).Name));
                
                NameSegment ns = e1 as NameSegment;

                dynamic_val = value;
                // sanity check so we wouldn't iterate a non enumerable
                if (dynamic_val is IEnumerable)
                {
                    foreach (var item in dynamic_val)
                    {
                        IncTotalBranchCount();
                        if (item.Name != form.Name)
                        {
                            AddLocal(tvds.Locals[0], item);
                            solution_list.Add(new Solution(this.Copy()));
                        }
                    }
                    return;
                }
                else // An incorrect value has been passed
                    Contract.Assert(false, Util.Error.MkErr(st, 1, "collection"));
            }



            IVariable declaration = tvds.Locals[0];

            NameSegment lhs_declaration = lhs as NameSegment;
            Contract.Assert(lhs_declaration != null, Util.Error.MkErr(st, 1, typeof(Expression)));

            // check that var on lhs is the same as rhs
            Contract.Assert(lhs_declaration.Name.Equals(declaration.Name), Util.Error.MkErr(st, 7));
            /* HACK
             * object value will be either a list<T> but T is unkown.
             * Or it will be a NameSegment
             * For now, cast it to dynamic type and pray.
             */
            ProcessArg(rhs, out value);

            dynamic_val = value;
            // sanity check so we wouldn't iterate a non enumerable
            if (dynamic_val is IEnumerable)
            {
                foreach (var item in dynamic_val)
                {
                    IncTotalBranchCount();
                    AddLocal(declaration, item);
                    solution_list.Add(new Solution(this.Copy()));
                }
            }
            else // An incorrect value has been passed
                Contract.Assert(false, Util.Error.MkErr(st, 1, "collection"));

            /* END HACK */
        }


        private void ResolveLhs(BinaryExpr bexp, IVariable declaration, out object result)
        {
            Contract.Requires(bexp != null && declaration != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            result = null;
            Expression lhs = bexp.E0;
            Expression rhs = bexp.E1;


            NameSegment lhs_declaration = lhs as NameSegment;
            if (lhs_declaration == null)
            {
                Util.Printer.Error(bexp, "Unexpected expression type after :|. Expected {0} Received {1}", typeof(BinaryExpr), lhs.GetType());
                return;
            }

            if (!lhs_declaration.Name.Equals(declaration.Name))
            {
                Util.Printer.Error(bexp, "Declared variable and variable after :| don't match. Expected {0} Received {1}", declaration.Name, lhs_declaration.Name);
                return;
            }
            /* HACK
             * object value will be either a list<T> but T is unkown.
             * Or it will be a NameSegment
             * For now, cast it to dynamic type and pray.
             */
            ProcessArg(rhs, out result);
        }
    }


}
