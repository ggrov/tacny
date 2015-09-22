using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    public interface IAtomicStmt
    {
        string Resolve(Statement st, ref List<Solution> solution_list);
    }

    public class Atomic
    {

        //public Solution solution;
        public Program program;

        public readonly GlobalContext globalContext;
        public LocalContext localContext;

        private Dictionary<Tactic, Atomic> tacticCache = new Dictionary<Tactic, Atomic>();
        protected Atomic(Atomic ac)
        {
            this.localContext = ac.localContext;
            this.globalContext = ac.globalContext;
            this.program = ac.globalContext.program;
        }

        public Atomic(MemberDecl md, Tactic tac, UpdateStmt tac_call, Program program)
        {
            Contract.Requires(md != null);
            Contract.Requires(tac != null);

            this.program = program;
            this.localContext = new LocalContext(md, tac, tac_call);
            this.globalContext = new GlobalContext(md, tac_call, program);
        }

        public Atomic(MemberDecl md, Tactic tac, UpdateStmt tac_call, GlobalContext globalContext)
        {
            this.localContext = new LocalContext(md, tac, tac_call);
            this.globalContext = globalContext;
            this.program = globalContext.program;

        }

        public Atomic(LocalContext localContext, GlobalContext globalContext, Dictionary<Tactic, Atomic> tacticCache)
        {
            this.program = globalContext.program.NewProgram();
            this.globalContext = globalContext;
            this.localContext = localContext.Copy();
            this.tacticCache = tacticCache;
        }

        /// <summary>
        /// Create a deep copy of an action
        /// </summary>
        /// <returns>Action</returns>
        public Atomic Copy()
        {
            return new Atomic(localContext, globalContext, tacticCache);
        }

        public virtual string FormatError(string err)
        {
            return "Error: " + err;
        }

        public static string ResolveTactic(Tactic tac, UpdateStmt tac_call, MemberDecl md, Program tacnyProgram, List<IVariable> variables, ref SolutionList result)
        {
            Contract.Requires(tac != null);
            Contract.Requires(tac_call != null);
            Contract.Requires(md != null);
            List<Solution> res = null;
            string err;
            if (result.plist.Count == 0)
            {
                res = new List<Solution>();
                Atomic ac = new Atomic(md, tac, tac_call, tacnyProgram);
                ac.globalContext.RegsiterGlobalVariables(variables);
                err = ResolveTactic(ac, ref res);
            }
            else
            {
                res = new List<Solution>(result.plist.ToArray());
                // update local and global contexts for each state
                foreach (var sol in res)
                {
                    MemberDecl target = sol.state.localContext.new_target == null ? md : sol.state.localContext.new_target;
                    sol.state.localContext = new LocalContext(target, tac, tac_call);
                    sol.state.globalContext.tac_call = tac_call;
                    sol.state.globalContext.md = target;
                }
                err = ResolveTactic(ref res);
            }
            

            result.AddRange(res);
            return err;

        }

        /// <summary>
        /// Resovle tactic body, given that previous tactic calls have been made
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static string ResolveTactic(ref List<Solution> result)
        {
            Contract.Requires(tcce.NonEmpty(result));
            string err = null;
            SolutionList solution_list = new SolutionList();
            solution_list.AddRange(result);
            while (true)
            {
                List<Solution> res = null;

                err = Atomic.ResolveStatement(ref res, solution_list.plist);
                if (err != null)
                    return err;

                if (res.Count > 0)
                    solution_list.AddRange(res);
                else
                    break;
            }

            result.AddRange(solution_list.plist);
            return null;
        }

        /// <summary>
        /// Resolve tactic body, given that no tactic calls have been made before
        /// </summary>
        /// <param name="atomic">The base atomic class</param>
        /// <param name="result">Result list</param>
        /// <returns>Error message</returns>
        public static string ResolveTactic(Atomic atomic, ref List<Solution> result)
        {
            Contract.Requires(atomic != null);
            //local solution list
            SolutionList solution_list = new SolutionList(new Solution(atomic));
            string err = null;

            while (true)
            {
                List<Solution> res = null;

                err = Atomic.ResolveStatement(ref res, solution_list.plist);
                if (err != null)
                    return err;

                if (res.Count > 0)
                    solution_list.AddRange(res);
                else
                    break;
            }

            result.AddRange(solution_list.plist);
            return null;
        }


        public static string ResolveStatement(ref List<Solution> result, List<Solution> solution_list)
        {
            string err = null;

            if (result == null)
                result = new List<Solution>();

            foreach (var solution in solution_list)
            {
                if (solution.state.localContext.IsResolved())
                    continue;

                err = solution.state.CallAction(solution.state.localContext.GetCurrentStatement(), ref result);
                if (err != null)
                    return err;

                if (solution_list.IndexOf(solution) == solution_list.Count - 1)
                {
                    foreach (var res in result)
                    {
                        if (res.parent == null)
                        {
                            res.parent = solution;
                            res.state.localContext.IncCounter();
                        }
                    }
                }
            }

            return err;
        }

        protected string ResolveBody(BlockStmt bs, out List<Solution> solution_list)
        {
            Contract.Requires(bs != null);
            string err;
            solution_list = new List<Solution>();

            foreach (var stmt in bs.Body)
            {
                err = this.CallAction(stmt, ref solution_list);
                if (err != null)
                    return err;
            }

            return null;
        }

        protected string CallAction(object call, ref List<Solution> solution_list)
        {
            System.Type type;
            Statement st = call as Statement;
            ApplySuffix aps;
            if (st != null)
                type = StatementRegister.GetStatementType(st);
            else
            {
                aps = call as ApplySuffix;
                if (aps != null)
                {
                    type = StatementRegister.GetStatementType(StatementRegister.GetAtomicType(aps.Lhs.tok.val));
                    st = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps)});
                }

                else
                    return "unexpected call argument: expected Statement or ApplySuffix; Received " + call.GetType();
            }
            if (type == null)
            {
                UpdateStmt us = st as UpdateStmt;
                if (us != null)
                {
                    if (program.IsTacticCall(us))
                    {
                        Atomic ac;
                        Tactic tac = program.GetTactic(us);
                        //if (tacticCache.ContainsKey(tac))
                        //ac = tacticCache[tac];
                        //else
                        ac = new Atomic(localContext.tac, tac, us, globalContext);

                        ExprRhs er = (ExprRhs)ac.localContext.tac_call.Rhss[0];
                        List<Expression> exps = ((ApplySuffix)er.Expr).Args;
                        Contract.Assert(exps.Count == ac.localContext.tac.Ins.Count);
                        for (int i = 0; i < exps.Count; i++)
                        {
                            ac.AddLocal(ac.localContext.tac.Ins[i], GetLocalValueByName(exps[i] as NameSegment));
                        }
                        List<Solution> sol_list = new List<Solution>();
                        string err = Atomic.ResolveTactic(ac, ref sol_list);
                        if (err != null)
                            return err;

                        foreach (var solution in sol_list)
                        {
                            Atomic action = this.Copy();
                            foreach (KeyValuePair<Statement, Statement> kvp in solution.state.GetResult())
                            {
                                action.AddUpdated(kvp.Key, kvp.Value);
                            }
                            solution_list.Add(new Solution(action));
                        }

                        //if (tacticCache.ContainsKey(tac))
                        //    tacticCache[tac] = ac;
                        //else
                        //    tacticCache.Add(tac, ac);

                        return null;
                    }
                }
                return CallDefaultAction(st, ref solution_list);
            }

            var qq = Activator.CreateInstance(type, new object[] { this }) as IAtomicStmt;

            if (qq == null)
                return CallDefaultAction(st, ref solution_list);
            //return "Atomic Statement does not inherit the AtomicStmt interface";

            return qq.Resolve(st, ref solution_list);
        }

        /// <summary>
        /// Clear local variables, and fill them with tactic arguments. Use with caution.
        /// </summary>
        public void FillTacticInputs()
        {
            localContext.FillTacticInputs();
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
            TacnyBlockStmt tbs = null;
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
                    call_arguments = GetCallArguments(us);
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
            else if ((tbs = st as TacnyBlockStmt) != null)
            {
                ParensExpression pe = tbs.Guard as ParensExpression;
                if (pe != null)
                    call_arguments = new List<Expression>() { pe.E };
                else
                    call_arguments = new List<Expression>() { tbs.Guard };
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
            NameSegment nameSegment = null;
            ApplySuffix aps = null;
            if ((nameSegment = argument as NameSegment) != null)
            {
                if (!HasLocalWithName(nameSegment))
                    return "Argument not passed";

                result = GetLocalValueByName(nameSegment.Name);
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
                string err = CallAction(vds, ref sol_list);
                if (err != null)
                    return err;
                result = localContext.local_variables[lv];
                localContext.local_variables.Remove(lv);

            }
            else
                result = argument;

            return null;
        }

        private string CallDefaultAction(Statement st, ref List<Solution> solution_list)
        {
            Atomic state = this.Copy();
            state.AddUpdated(st, st);
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

            while (index >= 0)
            {
                Statement stmt = m.Body.Body[index];

                WhileStmt ws = stmt as WhileStmt;
                if (ws != null)
                    return ws;

                index--;
            }
            return null;
        }

        protected static List<Expression> GetCallArguments(UpdateStmt us)
        {
            ExprRhs er = (ExprRhs)us.Rhss[0];
            return ((ApplySuffix)er.Expr).Args;
        }

        protected bool HasLocalWithName(NameSegment ns)
        {
            return localContext.HasLocalWithName(ns);
        }

        protected object GetLocalValueByName(NameSegment ns)
        {
            return localContext.GetLocalValueByName(ns.Name);
        }

        protected object GetLocalValueByName(string name)
        {
            return localContext.GetLocalValueByName(name);
        }

        protected IVariable GetLocalKeyByName(NameSegment ns)
        {
            return localContext.GetLocalKeyByName(ns.Name);
        }

        protected IVariable GetLocalKeyByName(string name)
        {
            return localContext.GetLocalKeyByName(name);
        }

        public void Fin()
        {
            globalContext.resolved.Clear();
            globalContext.resolved.AddRange(localContext.updated_statements.Values.ToArray());
            globalContext.new_target = localContext.new_target;
        }

        protected void AddLocal(IVariable lv, object value)
        {
            localContext.AddLocal(lv, value);
        }

        protected static Token CreateToken(string val, int line, int col)
        {
            var tok = new Token(line, col);
            tok.val = val;
            return tok;
        }

        public List<Statement> GetResolved()
        {
            return globalContext.resolved;
        }

        public UpdateStmt GetTacticCall()
        {
            return localContext.tac_call;
        }

        public void AddUpdated(Statement key, Statement value)
        {
            Contract.Requires(key != null && value != null);
            localContext.AddUpdated(key, value);
        }

        public void RemoveUpdated(Statement key)
        {
            Contract.Requires(key != null);
            localContext.RemoveUpdated(key);
        }

        public Statement GetUpdated(Statement key)
        {
            Contract.Requires(key != null);
            return localContext.GetUpdated(key);
        }

        public Dictionary<Statement, Statement> GetResult()
        {
            return localContext.updated_statements;
        }
    }
}


