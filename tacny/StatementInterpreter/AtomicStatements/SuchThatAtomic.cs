using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class SuchThatAtomic : Atomic, IAtomicStmt
    {
        public SuchThatAtomic(Atomic atomic) : base(atomic) { }



        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return SuchThat(st, ref solution_list);
        }

        private string SuchThat(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            VarDeclStmt vds = st as VarDeclStmt;
            if (vds == null)
                return String.Format("Unexpected statement type. Expected {0} Received {1}", typeof(VarDeclStmt), st.GetType());

            AssignSuchThatStmt suchThat = vds.Update as AssignSuchThatStmt;

            if (suchThat == null)
                return String.Format("Unexpected statement type. Expected {0} Received {1}", typeof(AssignSuchThatStmt), vds.Update.GetType());


            BinaryExpr bexp = suchThat.Expr as BinaryExpr;

            if (bexp == null)
                return String.Format("Unexpected statement type. Expected {0} Received {1}", typeof(BinaryExpr), suchThat.Expr.GetType());

            Expression lhs = bexp.E0;
            Expression rhs = bexp.E1;
            
            IVariable declaration = vds.Locals[0];

            NameSegment lhs_declaration = lhs as NameSegment;
            if (lhs_declaration == null)
                return String.Format("Unexpected expression type after :|. Expected {0} Received {1}", typeof(BinaryExpr), suchThat.Expr.GetType());

            if(!lhs_declaration.Name.Equals(declaration.Name))
                return String.Format("Declared variable and variable after :| don't match. Expected {0} Received {1}", declaration.Name, lhs_declaration.Name);

            object value;
            /* HACK
             * object value will be either a list<T> but T is unkown.
             * Or it will be a NameSegment
             * For now, cast it to dynamic type and pray.
             */
            ProcessArg(rhs, out value);

            dynamic dynamic_val = value;
            // sanity check so we wouldn't iterate a non enumerable
            if (dynamic_val is IEnumerable)
            {
                foreach (var item in dynamic_val)
                {
                    AddLocal(declaration, item);
                    solution_list.Add(new Solution(this.Copy()));
                }
            }
            else // An incorrect value has been passed
                return String.Format("Unexpected argument. :| expects a collection, received {0}", dynamic_val.GetType());
            
            /* END HACK */
            
            return null;
        }
    }
}
