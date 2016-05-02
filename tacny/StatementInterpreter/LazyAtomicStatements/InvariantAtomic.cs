using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;

namespace LazyTacny
{
    class InvariantAtomic : Atomic, IAtomicLazyStmt
    {
        public InvariantAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            switch (StatementRegister.GetAtomicType(st))
            {
                case StatementRegister.Atomic.ADD_INVAR:
                    return AddInvar(st, solution);
                case StatementRegister.Atomic.CREATE_INVAR:
                    return CreateInvar(st, solution);
                default:
                    throw new cce.UnreachableException();
            }
        }

        public IEnumerable<Solution> CreateInvar(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            Expression formula = null;
            MaybeFreeExpression invariant = null;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));


            foreach (var forlumla in ProcessStmtArgument(call_arguments[0]))
            {
                Contract.Assert(formula != null);
                invariant = new MaybeFreeExpression(formula);
                var ac = this.Copy();
                ac.AddLocal(lv, invariant);
                yield return new Solution(this.Copy());
            }
            yield break;
        }

        public IEnumerable<Solution> AddInvar(Statement st, Solution solution)
        {

            List<Expression> call_arguments = null;
            MaybeFreeExpression invariant = null;
            MaybeFreeExpression[] invar_arr = null;
            List<MaybeFreeExpression> invar = null;
            UpdateStmt us = null;

            us = st as UpdateStmt;

            InitArgs(st, out call_arguments);
            Contract.Assert(call_arguments != null);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

            foreach (var item in ProcessStmtArgument(call_arguments[0]))
            {
                invariant = item as MaybeFreeExpression;
                Contract.Assert(invariant != null, Util.Error.MkErr(st, 1, typeof(MaybeFreeExpression)));
                WhileStmt nws = null;

                WhileStmt ws = FindWhileStmt(staticContext.tac_call, staticContext.md);
                Contract.Assert(ws != null, Util.Error.MkErr(st, 11));
                // if we already added new invariants to the statement, use the updated statement instead
                nws = GetUpdated(ws) as WhileStmt;

                if (nws != null)
                    invar_arr = nws.Invariants.ToArray();
                else
                    invar_arr = ws.Invariants.ToArray();

                invar = new List<MaybeFreeExpression>(invar_arr);
                invar.Add(invariant);
                nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);
                AddUpdated(ws, nws);
                yield return AddNewStatement<WhileStmt>(nws, nws);
            }
            yield break;
        }
    }
}
