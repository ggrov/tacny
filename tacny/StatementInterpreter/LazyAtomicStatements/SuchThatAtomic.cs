using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using System.Diagnostics;

namespace LazyTacny
{
    class SuchThatAtomic : Atomic, IAtomicLazyStmt
    {
        public SuchThatAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            Debug.Indent();
            foreach (var item in SuchThat(st, solution))
            {
                yield return item;
            }
            Debug.Unindent();
            yield break;
        }

        private IEnumerable<Solution> SuchThat(Statement st, Solution solution)
        {
            TacticVarDeclStmt tvds = st as TacticVarDeclStmt;
            Contract.Assert(tvds != null, Util.Error.MkErr(st, 5, typeof(TacticVarDeclStmt), st.GetType()));

            AssignSuchThatStmt suchThat = tvds.Update as AssignSuchThatStmt;
            Contract.Assert(suchThat != null, Util.Error.MkErr(st, 5, typeof(AssignSuchThatStmt), tvds.Update.GetType()));

            BinaryExpr bexp = suchThat.Expr as BinaryExpr;
            Contract.Assert(bexp != null, Util.Error.MkErr(st, 5, typeof(BinaryExpr), suchThat.Expr.GetType()));

            
            foreach (var item in ResolveExpression(bexp, tvds.Locals[0]))
            {
                AddLocal(tvds.Locals[0], item);
                yield return new Solution(this.Copy());

            }
            yield break;
        }

        private IEnumerable<object> ResolveExpression(Expression expr, IVariable declaration)
        {
            Contract.Requires(expr != null);

            if (expr is BinaryExpr)
            {

                BinaryExpr bexp = expr as BinaryExpr;
                switch (bexp.Op)
                {
                    case BinaryExpr.Opcode.In:
                        NameSegment var = bexp.E0 as NameSegment;
                        Contract.Assert(var != null, Util.Error.MkErr(bexp, 6, declaration.Name));
                        Contract.Assert(var.Name == declaration.Name, Util.Error.MkErr(bexp, 6, var.Name));
                        foreach (var result in ProcessStmtArgument(bexp.E1))
                        {
                            if(result is IEnumerable)
                            {
                                dynamic resultList = result;
                                foreach(var item in resultList)
                                {
                                    yield return item;
                                }
                            }
                        }
                        yield break;

                    case BinaryExpr.Opcode.And:
                        // for each item in the resolved lhs of the expression
                        foreach (var item in ResolveExpression(bexp.E0, declaration))
                        {
                            Atomic copy = this.Copy();
                            copy.AddLocal(declaration, item);
                            // resolve the rhs expression
                            foreach (var res in copy.ProcessStmtArgument(bexp.E1))
                            {
                                Dafny.LiteralExpr lit = res as Dafny.LiteralExpr;
                                // sanity check
                                Contract.Assert(lit != null, Util.Error.MkErr(expr, 17));
                                if (lit.Value is bool)
                                {
                                    // if resolved to true
                                    if ((bool)lit.Value)
                                    {
                                        yield return item;
                                    }
                                }
                            }
                        }

                        yield break;
                }
            }
        }
    }
}
