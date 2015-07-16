using System.Collections.Generic;
using Microsoft.Dafny;

namespace Tacny
{
    class InvariantAction : Action
    {
        public InvariantAction(Action action) : base(action) { }

        public string CreateInvar(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            Expression formula = null;
            MaybeFreeExpression invariant = null;
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "create_invariant: " + err;

            if (call_arguments.Count != 1)
                return "create_invariant: Wrong number of method arguments; Expected 1 got " + call_arguments.Count;

            if (!HasLocalWithName(call_arguments[0] as NameSegment))
                return "create_invariant: Local variable " + ((NameSegment)call_arguments[0]).Name + " is undefined";

            formula = (Expression)GetLocalValueByName(call_arguments[0] as NameSegment);

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

            if (st is UpdateStmt)
                us = st as UpdateStmt;
            else
                return "add_invariant: does not have a return value";

            call_arguments = GetCallArguments(us);

            if (call_arguments.Count != 1)
                return "add_invariant: Wrong number of method arguments; Expected 1 got " + call_arguments.Count;

            Expression exp = call_arguments[0];
            if (exp is NameSegment)
            {
                invariant = (MaybeFreeExpression)GetLocalValueByName((NameSegment)exp);
                if (invariant == null)
                    return "add_invariant: Local variable " + exp.tok.val + " undefined";

            }
            else
                return "add_invariant: Wrong expression type; Received " + exp.GetType() + " Expected Dafny.NameSegment";

            Method m = (Method)md;
            WhileStmt nws = null;

            WhileStmt ws = FindWhileStmt(tac_call, md);
            if (ws == null)
                return "add_invariant: add_invariant can only be called from a while loop";
            // if we already added new invariants to the statement, use the updated statement instead
            if (updated_statements.ContainsKey(ws))
            {
                nws = (WhileStmt)updated_statements[ws];
                invar_arr = nws.Invariants.ToArray();
            }
            else
                invar_arr = ws.Invariants.ToArray();

            invar = new List<MaybeFreeExpression>(invar_arr);
            invar.Add(invariant);
            nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, invar, ws.Decreases, ws.Mod, ws.Body);

            if (!updated_statements.ContainsKey(ws))
                updated_statements.Add(ws, nws);
            else
                updated_statements[ws] = nws;

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
