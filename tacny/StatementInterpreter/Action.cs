using Microsoft.Dafny;
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
        public List<Statement> tac_body = new List<Statement>(); // body of the currently worked tactic
        public readonly UpdateStmt tac_call = null;  // call to the tactic
        public Solution solution;
        public Program program;

        protected readonly Dictionary<string, DatatypeDecl> global_variables = new Dictionary<string, DatatypeDecl>();
        protected Dictionary<Dafny.IVariable, object> local_variables = new Dictionary<Dafny.IVariable, object>();
        protected Dictionary<Statement, Statement> updated_statements = new Dictionary<Statement, Statement>();

        public List<Statement> resolved = new List<Statement>();

        public Action(Action ac)
        {
            this.md = ac.md;
            this.tac = ac.tac;
            this.tac_body = ac.tac_body;
            this.tac_call = ac.tac_call;
            this.global_variables = ac.global_variables;
            this.local_variables = ac.local_variables;
            this.updated_statements = ac.updated_statements;
            this.program = ac.program;
        }

        public Action(MemberDecl md, Tactic tac, UpdateStmt tac_call, Program program)
        {
            Contract.Requires(md != null);
            Contract.Requires(tac != null);

            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            this.tac_body = new List<Statement>(tac.Body.Body.ToArray());
            this.program = program;
            FillGlobals(program.globals);
            FillTacticInputs();
        }

        private Action(MemberDecl md, Tactic tac, List<Statement> tac_body, UpdateStmt tac_call, Program program,
            Dictionary<Dafny.IVariable, object> local_variables, Dictionary<string, DatatypeDecl> global_variables,
            Dictionary<Statement, Statement> updated_statements, List<Statement> resolved)
        {
            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            this.tac_body = new List<Statement>(tac_body.ToArray());
            this.program = program;     

            List<IVariable> lv_keys = new List<IVariable>(local_variables.Keys);
            List<object> lv_values = new List<object>(local_variables.Values);
            this.local_variables = lv_keys.ToDictionary(x => x, x => lv_values[lv_keys.IndexOf(x)]);

            List<Statement> us_keys = new List<Statement>(updated_statements.Keys);
            List<Statement> us_values = new List<Statement>(updated_statements.Values);
            this.updated_statements = us_keys.ToDictionary(x => x, x => us_values[us_keys.IndexOf(x)]);

            this.global_variables = global_variables;
            this.resolved = new List<Statement>(resolved.ToArray());
        }

        /// <summary>
        /// Create a deep copy of an action
        /// </summary>
        /// <returns>Action</returns>
        public Action Copy()
        {
            var m = (Method)md;
            return new Action(
                    new Method(m.tok, m.Name, m.HasStaticKeyword, m.IsGhost, m.TypeArgs, m.Ins, m.Outs, m.Req, m.Mod, m.Ens, m.Decreases, m.Body, m.Attributes, m.SignatureEllipsis),
                    new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword, tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens, tac.Decreases, tac.Body, tac.Attributes, tac.SignatureEllipsis),
                    tac_body, tac_call, program.newProgram(), local_variables, global_variables, updated_statements, resolved);
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
            Action ac = new Action(md, tac, tac.Body.Body, tac_call, program, local_variables, global_variables, updated_statements, resolved);
            ac.FillTacticInputs();
            return ac;
        }


        public virtual string FormatError(string err)
        {
            return "Error: " + err;
        }

        public static string ResolveOne(ref List<Solution> result, List<Solution> solution_list)
        {
            string err = null;

            if (result == null)
                result = new List<Solution>();

            foreach (var solution in solution_list)
            {
                if (solution.state.tac_body.Count < 1)
                    continue;

                solution.state.solution = solution;
                err = solution.state.CallAction(solution.state.tac_body[0], ref result);
                foreach (var res in result)
                    if (res.parent == null)
                        res.parent = solution;

                if (solution_list.IndexOf(solution) == solution_list.Count - 1)
                    solution.state.tac_body.RemoveAt(0);
            }

            return err;
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

        public string CallAction(object call, ref List<Solution> solution_list)
        {
            Contract.Requires(call != null);
            string err;
            Atomic type = Atomic.UNDEFINED;
            Statement st = call as Statement;
            ApplySuffix aps = null;
            if (st != null)
                type = GetStatementType(st);
            else
            {
                aps = call as ApplySuffix;
                if (aps != null)
                    type = GetStatementType(aps);
                else
                    return "unexpected call argument: expectet Statement or ApplySuffix; Received " + call.GetType();
            }
            switch (type)
            {
                case Atomic.CREATE_INVAR:
                    err = CallCreateInvariant(st, ref solution_list);
                    break;
                case Atomic.ADD_INVAR:
                    err = CallAddInvariant(st, ref solution_list);
                    break;
                case Atomic.REPLACE_SINGLETON:
                    err = CallSingletonAction(st, ref solution_list);
                    break;
                case Atomic.EXTRACT_GUARD:
                    err = ExtractGuard(st, ref solution_list);
                    break;
                case Atomic.REPLACE_OP:
                    err = CallOperatorAction(st, ref solution_list);
                    break;
                case Atomic.COMPOSITION:
                    err = CallCompositionAction(st, ref solution_list);
                    break;
                case Atomic.ADD_MATCH:
                    err = CallMatchAction((TacnyCasesBlockStmt)st, ref solution_list);
                    break;
                case Atomic.ADD_IF:
                    err = CallIfAction((TacnyIfBlockStmt)st, ref solution_list);
                    break;
                default:
                    err = CallDefaultAction(st, ref solution_list);
                    break;
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
        private void FillGlobals(List<DatatypeDecl> globals)
        {
            this.global_variables.Clear();
            foreach (DatatypeDecl tld in globals)
                this.global_variables.Add(tld.Name, tld);
        }

        protected string InitArgs(Statement st, out List<Expression> call_arguments)
        {
            IVariable lv;
            return InitArgs(st, out lv, out call_arguments);
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
            VarDeclStmt vds = null;
            UpdateStmt us = null;

            if ((vds = st as VarDeclStmt) != null)
            {
                if (vds.Locals.Count != 1)
                    return "Wrong number of method result arguments; Expected 1 got " + vds.Locals.Count;
                lv = vds.Locals[0];
                call_arguments = GetCallArguments(vds.Update as UpdateStmt);

            }
            else if ((us = st as UpdateStmt) != null)
            {
                if (us.Lhss.Count == 0)
                {
                    call_arguments = GetCallArguments(us);
                }
                else
                {
                    NameSegment ns = (NameSegment)us.Lhss[0];
                    if (HasLocalWithName(ns))
                    {
                        lv = GetLocalKeyByName(ns);
                        call_arguments = GetCallArguments(us);
                    }
                    else
                        return "Local variable " + ns.Name + " not declared";
                }
            }
            else
                return "Wrong number of method result arguments; Expected 1 got 0";

            return null;
        }

        protected string ProcessArg(Expression argument, out Expression result)
        {
            object tmp;
            string err = ProcessArg(argument, out tmp);
            result = (Expression)tmp;
            return err;
        }

        protected string ProcessArg(Expression argument, out object result)
        {
            result = null;
            NameSegment ns = null;
            ApplySuffix aps = null;
            if ((ns = argument as NameSegment) != null)
            {
                if (!HasLocalWithName(ns))
                    return "Argument not passed";

                result = GetLocalValueByName(ns);
            }
            else if ((aps = argument as ApplySuffix) != null)
            {
                // create a VarDeclStmt
                // first create an UpdateStmt
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                // create a unique local variable
                Dafny.LocalVariable lv = new Dafny.LocalVariable(aps.tok, aps.tok, aps.Lhs.tok.val, null, false);
                VarDeclStmt vds = new VarDeclStmt(us.Tok, us.EndTok, new List<Dafny.LocalVariable>() { lv }, us);
                List<Solution> sol_list = new List<Solution>();
                CallAction(vds, ref sol_list);
                result = local_variables[lv];
                local_variables.Remove(lv);

            }
            else
                result = argument;

            return null;
        }

        private string CallSingletonAction(Statement st, ref List<Solution> solution_list)
        {
            SingletonAction rs = new SingletonAction(this);
            return rs.Replace(st, ref solution_list);
        }

        private string CallAddInvariant(Statement st, ref List<Solution> solution_list)
        {
            InvariantAction ia = new InvariantAction(this);
            return ia.AddInvar(st, ref solution_list);
        }

        private string CallCreateInvariant(Statement st, ref List<Solution> solution_list)
        {
            InvariantAction ia = new InvariantAction(this);
            return ia.CreateInvar(st, ref solution_list);
        }

        private string CallOperatorAction(Statement st, ref List<Solution> solution_list)
        {
            OperatorAction oa = new OperatorAction(this);
            return oa.ReplaceOperator(st, ref solution_list);
        }

        private string CallMatchAction(TacnyCasesBlockStmt st, ref List<Solution> solution_list)
        {
            MatchAction ma = new MatchAction(this);
            return ma.GenerateMatch(st, ref solution_list);
        }

        private string CallCompositionAction(Statement st, ref List<Solution> solution_list)
        {
            CompositionAction ca = new CompositionAction(this);
            return ca.Composition(st, ref solution_list);
        }

        private string CallIfAction(TacnyIfBlockStmt st, ref List<Solution> solution_list)
        {
            IfAction ia = new IfAction(this);
            return ia.AddIf(st, ref solution_list);
        }

        private string CallDefaultAction(Statement st, ref List<Solution> solution_list)
        {
            Action state = this.Copy();
            state.updated_statements.Add(st, st);
            solution_list.Add(new Solution(state, null));
            return null;
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

                WhileStmt ws = stmt as WhileStmt;
                if (ws != null)
                    return ws;

                index--;
            }
            return null;
        }

        public string ExtractGuard(Statement st, ref List<Solution> solution_list)
        {
            WhileStmt ws = null;
            Dafny.IVariable lv = null;
            Expression guard = null;
            string err;
            List<Expression> call_arguments; // we don't care about this

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "extract_guard: " + err;

            Method m = (Method)md;

            ws = FindWhileStmt(tac_call, md);
            if (ws == null)
                return "extract_guard: extract_guard can only be called from a while loop";
            guard = ws.Guard;

            AddLocal(lv, guard);

            solution_list.Add(new Solution(this.Copy()));
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

        public string Fin()
        {
            // for now try to copy dict values
            Statement[] tmp = (Statement[])updated_statements.Values.ToArray().Clone();
            resolved = new List<Statement>(tmp);

            return null;
        }

        protected void AddLocal(IVariable lv, object value)
        {
            if (!local_variables.ContainsKey(lv))
                local_variables.Add(lv, value);
            else
                local_variables[lv] = value;
        }

        protected static Token CreateToken(string val, int line, int col)
        {
            var tok = new Token(line, col);
            tok.val = val;
            return tok;
        }
    }
}
