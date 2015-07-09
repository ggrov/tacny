﻿using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

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
            ADD_MATCH,
            ADD_IF,
        };

        public readonly MemberDecl md = null; // the Class Member from which the tactic has been called
        public readonly Tactic tac = null;  // The called tactic
        public readonly UpdateStmt tac_call = null;  // call to the tactic
        public readonly Program program;

        protected readonly Dictionary<string, DatatypeDecl> global_variables = new Dictionary<string, DatatypeDecl>();
        protected Dictionary<Dafny.IVariable, object> local_variables = new Dictionary<Dafny.IVariable, object>();
        protected Dictionary<Statement, Statement> updated_statements = new Dictionary<Statement, Statement>();

        public List<Statement> resolved = new List<Statement>();

        public Action(Action ac)
        {
            this.md = ac.md;
            this.tac = ac.tac;
            this.tac_call = ac.tac_call;
            this.global_variables = ac.global_variables;
            this.local_variables = ac.local_variables;
            this.updated_statements = ac.updated_statements;
            this.program = ac.program;
        }

        public Action(MemberDecl md, Tactic tac, UpdateStmt tac_call, Program program, List<TopLevelDecl> global_variables)
        {
            Contract.Requires(md != null);
            Contract.Requires(tac != null);

            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            this.program = program;
            FillGlobals(global_variables);
            FillTacticInputs();
        }

        private Action(MemberDecl md, Tactic tac, UpdateStmt tac_call, Program program,
            Dictionary<Dafny.IVariable, object> local_variables, Dictionary<string, DatatypeDecl> global_variables,
            Dictionary<Statement, Statement> updated_statements, List<Statement> resolved)
        {
            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            this.program = program;

            List<IVariable> lv_keys = new List<IVariable>(local_variables.Keys);
            List<object> lv_values = new List<object>(local_variables.Values);
            this.local_variables = lv_keys.ToDictionary(x => x, x => lv_values[lv_keys.IndexOf(x)]);

            List<Statement> us_keys = new List<Statement>(updated_statements.Keys);
            List<Statement> us_values = new List<Statement>(updated_statements.Values);
            this.updated_statements = us_keys.ToDictionary(x => x, x => us_values[us_keys.IndexOf(x)]);

            this.global_variables = global_variables;
            this.resolved = resolved;
        }

        /// <summary>
        /// Create a deep copy of an action
        /// </summary>
        /// <returns>Action</returns>
        public Action Copy()
        {
            return new Action(md, tac, tac_call, program, local_variables, global_variables, updated_statements, resolved);
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
            Action ac = new Action(md, tac, tac_call, program, local_variables, global_variables, updated_statements, resolved);
            ac.FillTacticInputs();
            return ac;
        }

        protected static Atomic GetStatementType(Statement st)
        {
            Contract.Requires(st != null);
            ExprRhs er;
            UpdateStmt us;

            if (st is IfStmt || st is WhileStmt)
                return Atomic.COMPOSITION;
            else if (st is TacnyIfBlockStmt)
                return Atomic.ADD_IF;
            else if (st is TacnyCasesBlockStmt)
                return Atomic.ADD_MATCH;
            else if (st is UpdateStmt)
                us = st as UpdateStmt;
            else if (st is VarDeclStmt)
                us = ((VarDeclStmt)st).Update as UpdateStmt;

            else
                return Atomic.UNDEFINED;

            er = (ExprRhs)us.Rhss[0];

            return GetStatementType(er.Expr as ApplySuffix);
        }

        protected static Atomic GetStatementType(ApplySuffix ass)
        {
            Contract.Requires(ass != null);
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
                    err = action.CallCreateInvariant(st, ref solution_tree);
                    break;
                case Atomic.ADD_INVAR:
                    err = action.CallAddInvariant(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_SINGLETON:
                    err = action.CallSingletonAction(st, ref solution_tree);
                    break;
                case Atomic.EXTRACT_GUARD:
                    err = action.ExtractGuard(st, ref solution_tree);
                    break;
                case Atomic.REPLACE_OP:
                    err = action.CallOperatorAction(st, ref solution_tree);
                    break;
                case Atomic.COMPOSITION:
                    err = action.CallCompositionAction(st, ref solution_tree);
                    break;
                case Atomic.ADD_MATCH:
                    err = action.CallMatchAction((TacnyCasesBlockStmt)st, ref solution_tree);
                    break;
                case Atomic.ADD_IF:
                    err = action.CallIfAction((TacnyIfBlockStmt)st, ref solution_tree);
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
        /// Clear global variables and popualte the dictionary with the provided list
        /// </summary>
        /// <param name="globals">Global variables</param>
        private void FillGlobals(List<TopLevelDecl> globals)
        {
            this.global_variables.Clear();
            foreach (TopLevelDecl tld in globals)
            {
                if (tld is DatatypeDecl /*|| tld is RedirectingTypeDecl*/) // TODO what is RedirectingType
                    this.global_variables.Add(tld.Name, (DatatypeDecl)tld);
            }
        }

        /// <summary>
        /// Extract statement arguments and local variable definition
        /// </summary>
        /// <param name="st">Atomic statement</param>
        /// <param name="lv">Local variable</param>
        /// <param name="call_arguments">List of arguments</param>
        /// <returns>Error message</returns>
        protected string InitArgs(Statement st, out IVariable lv, out List<Expression> call_arguments)
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
                if (us.Lhss.Count != 1)
                    return "Wrong number of method result arguments; Expected 1 got " + us.Lhss.Count;

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

        protected string ProcessArg(Expression argument, out Expression result)
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

        private string CallSingletonAction(Statement st, ref SolutionTree solution_tree)
        {
            SingletonAction rs = new SingletonAction(this);
            return rs.Replace(st, ref solution_tree);
        }

        private string CallAddInvariant(Statement st, ref SolutionTree solution_tree)
        {
            InvariantAction ia = new InvariantAction(this);
            return ia.AddInvar(st, ref solution_tree);
        }

        private string CallCreateInvariant(Statement st, ref SolutionTree solution_tree)
        {
            InvariantAction ia = new InvariantAction(this);
            return ia.CreateInvar(st, ref solution_tree);
        }

        private string CallOperatorAction(Statement st, ref SolutionTree solution_tree)
        {
            OperatorAction oa = new OperatorAction(this);
            return oa.ReplaceOperator(st, ref solution_tree);
        }

        private string CallMatchAction(TacnyCasesBlockStmt st, ref SolutionTree solution_tree)
        {
            MatchAction ma = new MatchAction(this);
            return ma.GenerateMatch(st, ref solution_tree);
        }

        private string CallCompositionAction(Statement st, ref SolutionTree solution_tree)
        {
            CompositionAction ca = new CompositionAction(this);
            return ca.Composition(st, ref solution_tree);
        }

        private string CallIfAction(TacnyIfBlockStmt st, ref SolutionTree solution_tree)
        {
            IfAction ia = new IfAction(this);
            return ia.AddIf(st, ref solution_tree);
        }
        /// <summary>
        /// Find closest while statement to the tactic call
        /// </summary>
        /// <param name="tac_stmt">Tactic call</param>
        /// <param name="member">Method</param>
        /// <returns>WhileStmt</returns>
        protected static WhileStmt FindWhileStmt(Statement tac_stmt, MemberDecl member)
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

        protected static List<Expression> GetCallArguments(UpdateStmt us)
        {
            ExprRhs er = (ExprRhs)us.Rhss[0];
            return ((ApplySuffix)er.Expr).Args;
        }

        protected bool HasLocalWithName(NameSegment ns)
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

        protected object GetLocalValueByName(NameSegment ns)
        {
            Contract.Requires(ns != null);
            return GetLocalValueByName(ns.Name);
        }

        protected object GetLocalValueByName(string name)
        {
            Contract.Requires(name != null || name != "");

            List<Dafny.IVariable> ins = new List<Dafny.IVariable>(local_variables.Keys);

            foreach (Dafny.IVariable lv in ins)
            {
                if (lv.Name == name)
                    return local_variables[lv];
            }

            return null;
        }

        protected IVariable GetLocalKeyByName(NameSegment ns)
        {
            Contract.Requires(ns != null);
            return GetLocalKeyByName(ns.Name);
        }

        protected IVariable GetLocalKeyByName(string name)
        {
            Contract.Requires(name != null);
            List<Dafny.IVariable> ins = new List<Dafny.IVariable>(local_variables.Keys);

            foreach (Dafny.IVariable lv in ins)
            {
                if (lv.DisplayName == name)
                    return lv;
            }
            return null;
        }

        protected DatatypeDecl GetGlobalWithName(NameSegment ns)
        {
            if (ns == null)
                return null;
            if (global_variables.ContainsKey(ns.Name))
                return global_variables[ns.Name];

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

        protected void BranchLocals(IVariable lv, object value, SolutionTree solution_tree, Statement st)
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
