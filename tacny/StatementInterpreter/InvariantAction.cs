using System.Collections.Generic;
using Microsoft.Dafny;

namespace Tacny
{
    class InvariantAction : Action, AtomicStmt
    {

        public string FormatError(string method, string error)
        {
            return "ERROR " + method + ": "  + error;
        }

        public InvariantAction(Action action) : base(action) { }

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
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return FormatError("create_invariant", err);

            if (call_arguments.Count != 1)
                return FormatError("create_invariant", "Wrong number of method arguments; Expected 1 got " + call_arguments.Count);

            err = ProcessArg(call_arguments[0], out formula);
            if (err != null)
                return FormatError("create_invariant", err);

            invariant = new MaybeFreeExpression(formula);

            AddLocal(lv, invariant);
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
            string err;

            if (st is UpdateStmt)
                us = st as UpdateStmt;
            else
                return FormatError("add_invariant", "does not have a return value");

            err = InitArgs(st, out call_arguments);
            if (err != null)
                return FormatError("add_invariant", err);

            if (call_arguments.Count != 1)
                return FormatError("add_invariant", "Wrong number of method arguments; Expected 1 got " + call_arguments.Count);

            object tmp;
            err = ProcessArg(call_arguments[0], out tmp);
            invariant = tmp as MaybeFreeExpression;
            if (invariant == null)
                return FormatError("add_invariant", "Incorrect input type expected Invariant received " + tmp.GetType());

            Method m = (Method)md;
            WhileStmt nws = null;

            WhileStmt ws = FindWhileStmt(tac_call, md);
            if (ws == null)
                return FormatError("add_invariant", "add_invariant can only be called from a while loop");
            // if we already added new invariants to the statement, use the updated statement instead
            nws = localContext.GetUpdated(ws) as WhileStmt;
            
            if (nws != null)
            {
                invar_arr = nws.Invariants.ToArray();    
            }
            else
                invar_arr = ws.Invariants.ToArray();

            invar = new List<MaybeFreeExpression>(invar_arr);
            invar.Add(invariant);
            nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);

            localContext.AddUpdated(ws, nws);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
