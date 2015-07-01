using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;


namespace Tacny
{
    public class Action
    {
        public enum Atomic
        {
            UNDEFINED = 0,
            ASSERT,
            ADD_INVAR,
            CREATE_INVAR,
            REPLACE_SINGLETON,
            EXTRACT_GUARD,
            REPLACE_OP,
            COMPOSITION,
            IS_VALID,
        };

        public readonly MemberDecl md; // the Class Member from which the tactic has been called
        public readonly Tactic tac;  // The called tactic
        public readonly UpdateStmt tac_call;  // call to the tactic

        private Dictionary<Dafny.IVariable, object> local_variables = new Dictionary<Dafny.IVariable, object>();
        private Dictionary<Statement, Statement> updated_statements = new Dictionary<Statement, Statement>();

        public List<Statement> resolved = new List<Statement>();

        public Action()
        { }

        public Action(MemberDecl md, Tactic tac, UpdateStmt tac_call)
        {
            Contract.Requires(md != null);
            Contract.Requires(tac != null);

            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            FillTacticInputs();
        }

        private Action(MemberDecl md, Tactic tac, UpdateStmt tac_call,
            Dictionary<Dafny.IVariable, object> local_variables,
            Dictionary<Statement, Statement> updated_statements,
            List<Statement> resolved)
        {
            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;

            List<IVariable> lv_keys = new List<IVariable>(local_variables.Keys);
            List<object> lv_values = new List<object>(local_variables.Values);
            this.local_variables = lv_keys.ToDictionary(x => x, x => lv_values[lv_keys.IndexOf(x)]);

            List<Statement> us_keys = new List<Statement>(updated_statements.Keys);
            List<Statement> us_values = new List<Statement>(updated_statements.Values);
            this.updated_statements = us_keys.ToDictionary(x => x, x => us_values[us_keys.IndexOf(x)]);
            this.resolved = resolved;
        }

        /// <summary>
        /// Create a deep copy of an action
        /// </summary>
        /// <returns>Action</returns>
        public Action Copy()
        {
            return new Action(md, tac, tac_call, local_variables, updated_statements, resolved);
        }

        /// <summary>
        /// Create a copy of action with new method, tactic and tactic call params
        /// </summary>
        /// <param name="md">Method from which the tactic was called</param>
        /// <param name="tac">Called tactic</param>
        /// <param name="tac_call">Tactic_call</param>
        /// <returns>Action</returns>
        public Action Update(MemberDecl md, Tactic tac, UpdateStmt tac_call)
        {
            Action ac = new Action(md, tac, tac_call, local_variables, updated_statements, resolved);
            ac.FillTacticInputs();
            return ac;
        }


        private static Atomic GetStatementType(Statement st)
        {
            ExprRhs er;
            UpdateStmt us;
            if (st is UpdateStmt)
                us = st as UpdateStmt;
            else if (st is VarDeclStmt)
                us = ((VarDeclStmt)st).Update as UpdateStmt;
            else if (st is IfStmt || st is WhileStmt)
                return Atomic.COMPOSITION;
            else
                return Atomic.UNDEFINED;

            er = (ExprRhs)us.Rhss[0];
            ApplySuffix ass = er.Expr as ApplySuffix;
            return GetStatementType(ass);
        }

        private static Atomic GetStatementType(ApplySuffix ass)
        {

            switch (ass.Lhs.tok.val)
            {
                case "add_assert":
                    return Atomic.ASSERT;
                case "add_invariant":
                    return Atomic.ADD_INVAR;
                case "create_invariant":
                    return Atomic.CREATE_INVAR;
                case "extract_guard":
                    return Atomic.EXTRACT_GUARD;
                case "replace_operator":
                    return Atomic.REPLACE_OP;
                case "replace_singleton":
                    return Atomic.REPLACE_SINGLETON;
                case "is_valid":
                    return Atomic.IS_VALID;
                default:
                    return Atomic.UNDEFINED;
            }
        }

        public static string CallAction(Statement st, Action action, ref SolutionTree solution_tree)
        {
            Contract.Requires(st != null);
            string err;
            switch (GetStatementType(st))
            {
                case Atomic.CREATE_INVAR:
                    err = action.CreateInvar(st, ref solution_tree);
                    break;
                case Atomic.ADD_INVAR:
                    err = action.AddInvar(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_SINGLETON:
                    err = action.ReplaceSingleton(st, ref solution_tree);
                    break;
                case Atomic.EXTRACT_GUARD:
                    err = action.ExtractGuard(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_OP:
                    err = action.ReplaceOperator(st, ref solution_tree);
                    break;
                case Atomic.COMPOSITION:
                    err = action.Composition(st, ref solution_tree);
                    break;
                case Atomic.IS_VALID:
                    err = action.IsValid(st, ref solution_tree);
                    break;
                default:
                    throw new cce.UnreachableException();
            }
            return err;
        }

        /// <summary>
        /// Clear local variables, and fill them with tactic arguments. Use with caution.
        /// </summary>
        public void FillTacticInputs()
        {
            local_variables.Clear();
            List<Expression> exps = GetCallArguments(tac_call);
            Contract.Assert(exps.Count == tac.Ins.Count);
            for (int i = 0; i < exps.Count; i++)
            {
                local_variables.Add(tac.Ins[i], exps[i]);
            }
        }

        /// <summary>
        /// Extract statement arguments and local variable definition
        /// </summary>
        /// <param name="st">Atomic statement</param>
        /// <param name="lv">Local variable</param>
        /// <param name="call_arguments">List of arguments</param>
        /// <returns>Error message</returns>
        private string InitArgs(Statement st, out IVariable lv, out List<Expression> call_arguments)
        {
            lv = null;
            call_arguments = null;
            if (st is VarDeclStmt)
            {
                VarDeclStmt vds = (VarDeclStmt)st;
                if (vds.Locals.Count != 1)
                    return "Wrong number of method result arguments; Expected 1 got " + vds.Locals.Count;
                lv = vds.Locals[0];
                call_arguments = GetCallArguments(vds.Update as UpdateStmt);

            }
            else if (st is UpdateStmt)
            {
                UpdateStmt us = (UpdateStmt)st;
                NameSegment ns = (NameSegment)us.Lhss[0];
                if (HasLocalWithName(ns))
                {
                    lv = GetLocalKeyByName(ns);
                    call_arguments = GetCallArguments(us);
                }
                else
                    return "Local variable " + ns.Name + " not declared";
            }
            else
                return "Wrong number of method result arguments; Expected 1 got 0";

            return null;
        }

        private string ProcessArg(Expression argument, out Expression result)
        {
            result = null;
            if (argument is NameSegment)
            {
                if (!HasLocalWithName(argument as NameSegment))
                    return "Argument not passed";

                result = (Expression)GetLocalValueByName(argument as NameSegment);
            }
            else
                result = argument;

            return null;
        }

        /// <summary>
        /// Find closest while statement to the tactic call
        /// </summary>
        /// <param name="tac_stmt">Tactic call</param>
        /// <param name="member">Method</param>
        /// <returns>WhileStmt</returns>
        private static WhileStmt FindWhileStmt(Statement tac_stmt, MemberDecl member)
        {
            Method m = (Method)member;

            int index = m.Body.Body.IndexOf(tac_stmt);
            if (index <= 0)
                return null;

            while (index > 0)
            {
                Statement stmt = m.Body.Body[index];

                if (stmt is WhileStmt)
                    return (WhileStmt)stmt;
                index--;

            }

            return null;
        }

        /// <summary>
        /// Replace a singleton with a new term
        /// </summary>
        /// <param name="st">replace_singleton(); Statement</param>
        /// <param name="solution_tree">Reference to the solution tree</param>
        /// <returns> null if success; error message otherwise</returns>
        public string ReplaceSingleton(Statement st, ref SolutionTree solution_tree)
        {
            Dafny.IVariable lv = null;
            List<Expression> call_arguments = null;
            List<Expression> processed_args = new List<Expression>(3);
            Expression old_singleton = null;
            Expression new_term = null;
            Expression formula = null;
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "replace_singleton: " + err;

            if (call_arguments.Count != 3)
                return "replace_singleton: Wrong number of method arguments; Expected 3 got " + call_arguments.Count;

            err = ProcessArg(call_arguments[0], out new_term);
            if (err != null)
                return "replace_singleton: " + err;

            err = ProcessArg(call_arguments[1], out old_singleton);
            if (err != null)
                return "replace_singleton: " + err;

            err = ProcessArg(call_arguments[2], out formula);
            if (err != null)
                return "replace_singleton: " + err;

            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);

            List<Expression> exp_list = new List<Expression>();

            err = ReplaceTerm(old_singleton, new_term, et, ref exp_list);
            if (err != null)
                return err;
            // branch
            if (exp_list.Count > 0)
            {
                for (int i = 0; i < exp_list.Count; i++)
                {
                    BranchLocals(lv, exp_list[i], solution_tree, st);
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

        public string ReplaceOperator(Statement st, ref SolutionTree solution_tree)
        {
            Dafny.IVariable lv = null;
            List<Expression> call_arguments = null;
            StringLiteralExpr old_operator = null;
            StringLiteralExpr new_operator = null;
            Expression formula = null;
            BinaryExpr.Opcode old_op;
            BinaryExpr.Opcode new_op;
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "replace_operator: " + err;

            if (call_arguments.Count != 3)
                return "replace_operator: Wrong number of method arguments; Expected 3 got " + call_arguments.Count;


            old_operator = (StringLiteralExpr)call_arguments[0];
            new_operator = (StringLiteralExpr)call_arguments[1];
            if (call_arguments[2] is NameSegment)
            {
                if (!HasLocalWithName(call_arguments[2] as NameSegment))
                    return ""; // formula not passed
                else
                    formula = (Expression)GetLocalValueByName(call_arguments[2] as NameSegment);
            }
            else if (call_arguments[2] is Expression)
            {
                formula = (Expression)call_arguments[2];
            }

            try
            {
                old_op = ToOpCode((string)old_operator.Value);
                new_op = ToOpCode((string)new_operator.Value);
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }

            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);
            List<Expression> exp_list = new List<Expression>();

            ReplaceOp(old_op, new_op, et, ref exp_list);

            if (exp_list.Count == 0)
                exp_list.Add(formula);

            // smells like unnecessary branching if no replacement happened.
            for (int i = 0; i < exp_list.Count; i++)
            {
                BranchLocals(lv, exp_list[i], solution_tree, st);
            }

            return null;
        }

        protected Expression ReplaceOp(BinaryExpr.Opcode old_op, BinaryExpr.Opcode new_op, ExpressionTree formula, ref List<Expression> nexp)
        {
            Contract.Requires(nexp != null);
            if (formula == null)
                return null;

            if (formula.data is BinaryExpr)
            {
                if (((BinaryExpr)formula.data).Op == old_op)
                {
                    ExpressionTree nt = formula.Copy();
                    nt.data = new BinaryExpr(formula.data.tok, new_op, ((BinaryExpr)formula.data).E0, ((BinaryExpr)formula.data).E1);
                    nexp.Add(nt.root.TreeToExpression());
                    return null;
                }
            }
            ReplaceOp(old_op, new_op, formula.lChild, ref nexp);
            ReplaceOp(old_op, new_op, formula.rChild, ref nexp);
            return null;
        }

        protected BinaryExpr.Opcode ToOpCode(string op)
        {
            foreach (BinaryExpr.Opcode code in Enum.GetValues(typeof(BinaryExpr.Opcode)))
            {
                try
                {
                    if (BinaryExpr.OpcodeString(code) == op)
                        return code;
                }
                catch (cce.UnreachableException)
                {
                    throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
                }

            }
            throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
        }

        public string CreateInvar(Statement st, ref SolutionTree solution_tree)
        {
            Dafny.IVariable lv = null;
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

            BranchLocals(lv, invariant, solution_tree, st);
            return null;
        }

        public string ExtractGuard(Statement st, ref SolutionTree solution_tree)
        {
            WhileStmt ws = null;
            Dafny.IVariable lv = null;
            Expression guard = null;
            string err;
            List<Expression> call_arguments; // we don't care about this

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "replace_operator: " + err;

            Method m = (Method)md;

            ws = FindWhileStmt(tac_call, md);
            if (ws == null)
                return "extract_guard: extract_guard can only be called from a while loop";
            guard = ws.Guard;

            BranchLocals(lv, guard, solution_tree, st);
            return null;
        }

        public string AddInvar(Statement st, ref SolutionTree solution_tree)
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

            solution_tree.AddChild(new SolutionTree(this, solution_tree, st));
            return null;
        }


        public string Composition(Statement st, ref SolutionTree solution_tree)
        {
            IfStmt if_stmt = null;
            WhileStmt while_stmt = null;
            string err;

            if (st is IfStmt)
                if_stmt = (IfStmt)st;
            else if (st is WhileStmt)
                while_stmt = (WhileStmt)st;
            else
                return "composition: Internal error unexpected Statement type: " + st.GetType();

            if (if_stmt != null)
            {
                Expression guard = if_stmt.Guard;
                Atomic guard_type;
                // get guard type
                err = AnalyseGuard(guard, out guard_type);
                if (err != null)
                    return "composition: " + err;
                
                // call the matching statement
                // depending on the results analyse the required body.
            }
            return null;
        }

        /// <summary>
        /// Forces current node verification
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_tree"></param>
        /// <returns></returns>
        public string IsValid(Statement st, ref SolutionTree solution_tree)
        {
            if (!solution_tree.isLeaf())
                solution_tree = solution_tree.GetLeftMost();

            //Dafny.Program prog = solution_tree.GenerateProgram
            return null;
        }
        
        private string AnalyseGuard(Expression guard, out Atomic type)
        {
            Expression exp;
            type = Atomic.UNDEFINED;

            if (guard is ParensExpression)
                exp = ((ParensExpression)guard).E;
            else
                exp = guard;

            if (exp is ApplySuffix)
                type = GetStatementType((ApplySuffix)exp);
            else
                return "Invalid composition guard; Expected atomic statement; Received " + exp.GetType();

            return null;
        }

        private static List<Expression> GetCallArguments(UpdateStmt us)
        {
            ExprRhs er = (ExprRhs)us.Rhss[0];
            return ((ApplySuffix)er.Expr).Args;
        }

        private bool HasLocalWithName(NameSegment ns)
        {
            if (ns == null)
                return false;

            List<Dafny.IVariable> ins = new List<Dafny.IVariable>(local_variables.Keys);

            foreach (Dafny.IVariable lv in ins)
            {
                if (lv.Name == ns.Name)
                    return true;
            }

            return false;
        }

        private object GetLocalValueByName(NameSegment ns)
        {
            Contract.Requires(ns != null);

            List<Dafny.IVariable> ins = new List<Dafny.IVariable>(local_variables.Keys);

            foreach (Dafny.IVariable lv in ins)
            {
                if (lv.Name == ns.Name)
                    return local_variables[lv];
            }

            return null;
        }

        private Dafny.IVariable GetLocalKeyByName(NameSegment ns)
        {
            Contract.Requires(ns != null);
            List<Dafny.IVariable> ins = new List<Dafny.IVariable>(local_variables.Keys);

            foreach (Dafny.IVariable lv in ins)
            {
                if (lv.DisplayName == ns.Name)
                    return lv;
            }
            return null;
        }

        public string Finalize(ref SolutionTree solution_tree)
        {       
            // for now try to copy dict values
            resolved = new List<Statement>(updated_statements.Values);
            SolutionTree st = new SolutionTree(this, solution_tree);
            st.isFinal = true;
            solution_tree.AddChild(st);
            return null;
        }

        private void BranchLocals(IVariable lv, object value, SolutionTree solution_tree, Statement st)
        {
            Action ac = this.Copy();
            if (!ac.local_variables.ContainsKey(lv))
                ac.local_variables.Add(lv, value);
            else
                ac.local_variables[lv] = value;

            solution_tree.AddChild(new SolutionTree(ac, solution_tree, st));
        }
    }
}
