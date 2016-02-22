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
using Tacny;

namespace LazyTacny
{
    class SuchThatAtomic : Atomic, IAtomicLazyStmt
    {
        public SuchThatAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<Solution> SuchThat(Statement st, Solution solution)
        {
            object value = null;
            dynamic dynamic_val = null;
            TacticVarDeclStmt tvds = st as TacticVarDeclStmt;
            Contract.Assert(tvds != null, Util.Error.MkErr(st, 5, typeof(TacticVarDeclStmt), st.GetType()));

            AssignSuchThatStmt suchThat = tvds.Update as AssignSuchThatStmt;
            Contract.Assert(suchThat != null, Util.Error.MkErr(st, 5, typeof(AssignSuchThatStmt), tvds.Update.GetType()));

            BinaryExpr bexp = suchThat.Expr as BinaryExpr;
            Contract.Assert(bexp != null, Util.Error.MkErr(st, 5, typeof(BinaryExpr), suchThat.Expr.GetType()));
            ResolveExpression(bexp, tvds.Locals[0], out value);
            dynamic_val = value;
            if (dynamic_val is IEnumerable)
            {
                foreach (var item in dynamic_val)
                {
                    AddLocal(tvds.Locals[0], item);
                    yield return new Solution(this.Copy());
                }
            }
        }

        private void ResolveExpression(Expression expr, IVariable declaration, out object result)
        {
            Contract.Requires(expr != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);

            if (expr is BinaryExpr)
            {

                BinaryExpr bexp = expr as BinaryExpr;
                switch (bexp.Op)
                {
                    case BinaryExpr.Opcode.In:
                        NameSegment var = bexp.E0 as NameSegment;
                        Contract.Assert(var != null, Util.Error.MkErr(bexp, 6, declaration.Name));
                        Contract.Assert(var.Name == declaration.Name, Util.Error.MkErr(bexp, 6, var.Name));
                        ProcessArg(bexp.E1, out result);
                        return;

                    case BinaryExpr.Opcode.And:
                        object lhs_res;
                        // resolve lhs of the expression
                        ResolveExpression(bexp.E0, declaration, out lhs_res);
                        dynamic lhs_dres = lhs_res;
                        if (lhs_dres is IEnumerable)
                        {
                            List<dynamic> tmp = new List<dynamic>();
                            foreach (var item in lhs_dres)
                            {
                                Atomic copy = this.Copy();
                                copy.AddLocal(declaration, item);
                                object res;
                                copy.ProcessArg(bexp.E1, out res);
                                Dafny.LiteralExpr lit = res as Dafny.LiteralExpr;
                                Contract.Assert(lit != null, Util.Error.MkErr(expr, 17));
                                if (lit.Value is bool)
                                {
                                    if ((bool)lit.Value)
                                    {
                                        tmp.Add(item);
                                    }
                                }
                            }
                            result = tmp;
                        }
                        // apply the constraints to the results
                        //ResolveExpression(bexp.E1, declaration, out res_2);
                        break;
                }
            }
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
