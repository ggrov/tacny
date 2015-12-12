using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
namespace Tacny
{
    class InvariantAtomic : Atomic, IAtomicStmt
    {

        public string FormatError(string method, string error)
        {
            return "ERROR " + method + ": " + error;
        }

        public InvariantAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            switch (StatementRegister.GetAtomicType(st))
            {
                case StatementRegister.Atomic.ADD_INVAR:
                    return AddInvar(st, ref solution_list);
                case StatementRegister.Atomic.CREATE_INVAR:
                    return CreateInvar(st, ref solution_list);
                default:
                    throw new cce.UnreachableException();
            }
        }

        public string CreateInvar(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            Expression formula = null;
            MaybeFreeExpression invariant = null;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

            ProcessArg(call_arguments[0], out formula);
            Contract.Assert(formula != null);

            invariant = new MaybeFreeExpression(formula);

            AddLocal(lv, invariant);
            IncTotalBranchCount();
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }

        public string AddInvar(Statement st, ref List<Solution> solution_list)
        {

            List<Expression> call_arguments = null;
            MaybeFreeExpression invariant = null;
            MaybeFreeExpression[] invar_arr = null;
            List<MaybeFreeExpression> invar = null; // HACK
            UpdateStmt us = null;

            us = st as UpdateStmt;

            InitArgs(st, out call_arguments);
            Contract.Assert(call_arguments != null);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

            object invar_obj;
            ProcessArg(call_arguments[0], out invar_obj);
            invariant = invar_obj as MaybeFreeExpression;
            Contract.Assert(invar_obj != null, Util.Error.MkErr(st, 1, typeof(MaybeFreeExpression)));
            WhileStmt nws = null;

            WhileStmt ws = FindWhileStmt(globalContext.tac_call, globalContext.md);
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
            IncTotalBranchCount();
            AddUpdated(ws, nws);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
