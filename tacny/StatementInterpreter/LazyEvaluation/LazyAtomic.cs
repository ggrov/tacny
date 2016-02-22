using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System.Numerics;
using Tacny;


namespace LazyTacny
{
    [ContractClass(typeof(AtomicLazyContract))]
    public interface IAtomicLazyStmt
    {
        IEnumerable<Solution> Resolve(Statement st, Solution solution);
    }

    [ContractClassFor(typeof(IAtomicLazyStmt))]
    // Validate the input before execution
    public abstract class AtomicLazyContract : IAtomicLazyStmt
    {

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            Contract.Requires<ArgumentNullException>(st != null && solution != null);
            yield return null; // is null a valid default yield return val?
        }
    }

    public class Atomic
    {
        public readonly StaticContext globalContext;
        public LocalContext localContext;

        protected Atomic(Atomic ac)
        {
            Contract.Requires(ac != null);

            this.localContext = ac.localContext;
            this.globalContext = ac.globalContext;
        }

        public Atomic(MemberDecl md, Tactic tac, UpdateStmt tac_call, Tacny.Program program)
        {
            Contract.Requires(md != null);
            Contract.Requires(tac != null);

            this.localContext = new LocalContext(md, tac, tac_call);
            this.globalContext = new StaticContext(md, tac_call, program);
        }

        public Atomic(MemberDecl md, Tactic tac, UpdateStmt tac_call, StaticContext globalContext)
        {
            this.localContext = new LocalContext(md, tac, tac_call);
            this.globalContext = globalContext;

        }

        public Atomic(LocalContext localContext, StaticContext globalContext)
        {
            this.globalContext = globalContext;
            this.localContext = localContext.Copy();
        }

        /// <summary>
        /// Create a deep copy of an action
        /// </summary>
        /// <returns>Action</returns>
        public Atomic Copy()
        {
            Contract.Ensures(Contract.Result<Atomic>() != null);
            return new Atomic(localContext, globalContext);
        }


        public static Solution ResolveTactic(Tactic tac, UpdateStmt tac_call, MemberDecl md, Tacny.Program tacnyProgram, List<IVariable> variables, List<IVariable> resolved, SolutionList result)
        {
            Contract.Requires(tac != null);
            Contract.Requires(tac_call != null);
            Contract.Requires(md != null);
            Contract.Requires(tacnyProgram != null);
            Contract.Requires(tcce.NonNullElements<IVariable>(variables));
            Contract.Requires(tcce.NonNullElements<IVariable>(resolved));
            Contract.Requires(result != null);
            List<Solution> res = new List<Solution>();

            if (result.plist.Count == 0)
            {
                Atomic ac = new Atomic(md, tac, tac_call, tacnyProgram);
                ac.globalContext.RegsiterGlobalVariables(variables, resolved);
                // because solution is verified at the end
                // we can safely terminated after the first item is received
                // TODO
                var penum = ResolveTactic(res, ac).GetEnumerator();
                penum.MoveNext();
                return penum.Current;
            }
            else
            {
                // update local and global contexts for each state
                foreach (var sol in result.plist)
                {
                    MemberDecl target = sol.state.localContext.new_target == null ? md : sol.state.localContext.new_target;
                    StaticContext gc = sol.state.globalContext;
                    gc.tac_call = tac_call;
                    gc.md = target;
                    // clean up old globals
                    gc.ClearGlobalVariables();
                    // register new globals
                    gc.RegsiterGlobalVariables(variables);

                    Atomic ac = new Atomic(target, tac, tac_call, gc);
                    res.Add(new Solution(ac));
                }
                ResolveTactic(res);
            }


            result.AddRange(res);

            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="atomic"></param>
        /// <returns></returns>
        /*
            !!!Search strategies will go in here!!!
            !!! Validation of the results should also go here!!!
         */
        public static IEnumerable<Solution> ResolveTactic(List<Solution> input, Atomic atomic = null, bool verify = true)
        {
            Contract.Requires(input != null);
            //local solution list
            SolutionList solutionList = atomic == null ? new SolutionList() : new SolutionList(new Solution(atomic));
            // if previous solutions exist, add them 
            if (input.Count > 0)
                solutionList.AddRange(input);
            // BFS startegy
            while (true)
            {
                List<Solution> temp = new List<Solution>();
                // iterate every solution
                foreach (var item in solutionList.plist)
                {
                    // lazily resolve a statement in the solution
                    foreach (var solution in ResolveStatement(item))
                    {
                        // validate result
                        if (solution.IsResolved())
                        {
                            if (verify)
                            {
                                // if verifies break else continue
                                solution.state.GenerateAndVerify(solution);
                                if (!solution.state.globalContext.program.HasError())
                                {
                                    yield return solution;
                                    // return the valid solution and terminate
                                    yield break;
                                }
                            }
                            else
                            {
                                yield return solution;
                            }

                        }
                        else
                        {
                            temp.Add(solution);
                        }
                    }
                    // if no branches were generated break
                    if (temp.Count == 0)
                        yield break;
                    solutionList.AddRange(temp);
                }
            }
        }

        /// <summary>
        /// Resolve a block statement
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        protected IEnumerable<Solution> ResolveBody(BlockStmt body)
        {
            Contract.Requires<ArgumentNullException>(body != null);

            Atomic ac = this.Copy();
            ac.localContext.tac_body = body.Body;
            ac.localContext.ResetCounter();
            List<Solution> result = new List<Solution>();
            foreach (var item in ResolveTactic(result, ac, false))
            {
                yield return item;
            }
            yield break;
        }

        public static IEnumerable<Solution> ResolveStatement(Solution solution)
        {
            Contract.Requires<ArgumentNullException>(solution != null);
            if (solution.state.localContext.IsResolved())
                yield break;
            // if no statements have been resolved, check the preconditions
            if (solution.state.localContext.IsFirstStatment())
                TacnyContract.ValidateRequires(solution);
            // foreach result yield
            foreach (var result in solution.state.CallAction(solution.state.localContext.GetCurrentStatement(), solution))
            {
                if (result.parent == null)
                {
                    result.parent = solution;
                    result.state.localContext.IncCounter();
                    yield return result;
                }
            }


            yield break;
        }

        protected IEnumerable<Solution> CallAction(object call, Solution solution)
        {
            Contract.Requires<ArgumentNullException>(call != null);
            Contract.Requires<ArgumentNullException>(solution != null);
            System.Type type = null;
            Statement st;
            ApplySuffix aps;
            UpdateStmt us;

            if ((st = call as Statement) != null)
                type = StatementRegister.GetStatementType(st);
            else
            {
                if ((aps = call as ApplySuffix) != null)
                {
                    type = StatementRegister.GetStatementType(aps);
                    st = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                }
            }
            /**
             * Unrecognized statement type, check if it's a nested tactic call
             */
            if (type == null)
            {
                if ((us = st as UpdateStmt) != null)
                {
                    // if the statement is nested tactic call
                    if (globalContext.program.IsTacticCall(us))
                    {
                        Atomic ac = new Atomic(localContext.md, globalContext.program.GetTactic(us), us, globalContext);

                        ExprRhs er = (ExprRhs)ac.localContext.tac_call.Rhss[0];
                        List<Expression> exps = ((ApplySuffix)er.Expr).Args;
                        Contract.Assert(exps.Count == ac.localContext.tac.Ins.Count);
                        ac.SetNewTarget(GetNewTarget());
                        for (int i = 0; i < exps.Count; i++)
                        {
                            foreach(var result in ProcessStmtArgument(exps[i]))
                            {
                                ac.AddLocal(ac.localContext.tac.Ins[i], result);
                            }
                        }
                        List<Solution> sol_list = new List<Solution>();
                        /**
                         * Transfer the results from evaluating the nested tactic
                         */
                        foreach (var item in ResolveTactic(sol_list, ac, false))
                        {
                            Atomic action = this.Copy();
                            action.SetNewTarget(solution.state.GetNewTarget());
                            foreach (KeyValuePair<Statement, Statement> kvp in solution.state.GetResult())
                                action.AddUpdated(kvp.Key, kvp.Value);
                            yield return item;
                        }
                        yield break;
                    }
                    else // insert the statement as is
                    {
                        yield return CallDefaultAction(st);
                        yield break;
                    }
                }
                var tvds = st as TacticVarDeclStmt;
                // if empty variable declaration
                // register variable to local with empty value
                if (tvds != null)
                {
                    yield return RegisterLocalVariable(tvds);
                    yield break;
                }
                else
                {
                    yield return CallDefaultAction(st);
                    yield break;
                }
            }
            else
            {
                var qq = Activator.CreateInstance(type, new object[] { this }) as IAtomicLazyStmt;
                if (qq == null) // unrecognized statement, insert as is
                {
                    yield return CallDefaultAction(st);
                    yield break;
                }
                else
                {
                    foreach (var res in qq.Resolve(st, solution))
                    {
                        yield return res;
                    }

                    yield break;
                }
            }
        }

        public IEnumerable<object> ProcessStmtArgument(Expression argument)
        {
            Contract.Requires<ArgumentNullException>(argument != null);
            NameSegment ns = null;
            ApplySuffix aps = null;
            if ((ns = argument as NameSegment) != null)
            {
                Contract.Assert(HasLocalWithName(ns), Util.Error.MkErr(ns, 9, ns.Name));
                yield return GetLocalValueByName(ns.Name);
                yield break;
            }
            else if ((aps = argument as ApplySuffix) != null)
            {
                // create a VarDeclStmt
                // first create an UpdateStmt
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                // create a unique local variable
                Dafny.LocalVariable lv = new Dafny.LocalVariable(aps.tok, aps.tok, aps.Lhs.tok.val, new BoolType(), false);
                TacticVarDeclStmt tvds = new TacticVarDeclStmt(us.Tok, us.EndTok, new List<Dafny.LocalVariable>() { lv }, us);
                List<Solution> sol_list = new List<Solution>();
                foreach (var item in CallAction(tvds, new Solution(this.Copy())))
                {
                    var res = item.state.localContext.local_variables[lv];
                    item.state.localContext.local_variables.Remove(lv);
                    yield return res;
                }
                yield break;
            }
            else if (argument is BinaryExpr || argument is ParensExpression)
            {
                ExpressionTree expt = ExpressionTree.ExpressionToTree(argument);
                ResolveExpression(expt);
                if (IsResolvable(expt))
                    yield return EvaluateExpression(expt);
                else
                    yield return expt.TreeToExpression();
                yield break;
            }
            else
            {
                yield return argument;
                yield break;
            }
        }

        private Solution RegisterLocalVariable(TacticVarDeclStmt declaration)
        {
            Contract.Requires(declaration != null);
            // if declaration has rhs
            if (declaration.Update != null)
            {
                UpdateStmt rhs = declaration.Update as UpdateStmt;
                // if statement is of type var q;
                if (rhs == null)
                { /* leave the statement */ }
                else
                {
                    foreach (var item in rhs.Rhss)
                    {
                        int index = rhs.Rhss.IndexOf(item);
                        Contract.Assert(declaration.Locals.ElementAtOrDefault(index) != null, Util.Error.MkErr(declaration, 8));
                        ExprRhs exprRhs = item as ExprRhs;
                        // if the declaration is literal expr (e.g. var q := 1)
                        Dafny.LiteralExpr litExpr = exprRhs.Expr as Dafny.LiteralExpr;
                        if (litExpr != null)
                            AddLocal(declaration.Locals[index], litExpr);
                        else
                        {
                            foreach(var result in ProcessStmtArgument(exprRhs.Expr))
                            {
                                AddLocal(declaration.Locals[index], result);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var item in declaration.Locals)
                    AddLocal(item as IVariable, null);
            }
            return new Solution(this.Copy());
        }

        /// <summary>
        /// Clear local variables, and fill them with tactic arguments. Use with caution.
        /// </summary>
        public void FillTacticInputs()
        {
            localContext.FillTacticInputs();
        }

        protected void InitArgs(Statement st, out List<Expression> call_arguments)
        {
            Contract.Requires(st != null);
            Contract.Ensures(Contract.ValueAtReturn<List<Expression>>(out call_arguments) != null);
            IVariable lv;
            InitArgs(st, out lv, out call_arguments);
        }

        /// <summary>
        /// Extract statement arguments and local variable definition
        /// </summary>
        /// <param name="st">Atomic statement</param>
        /// <param name="lv">Local variable</param>
        /// <param name="call_arguments">List of arguments</param>
        /// <returns>Error message</returns>
        protected void InitArgs(Statement st, out IVariable lv, out List<Expression> call_arguments)
        {
            Contract.Requires(st != null);
            Contract.Ensures(Contract.ValueAtReturn<List<Expression>>(out call_arguments) != null);
            lv = null;
            call_arguments = null;
            TacticVarDeclStmt tvds = null;
            UpdateStmt us = null;
            TacnyBlockStmt tbs = null;
            // tacny variables should be declared as tvar or tactic var
            if (st is VarDeclStmt)
                Contract.Assert(false, Util.Error.MkErr(st, 13));

            if ((tvds = st as TacticVarDeclStmt) != null)
            {
                lv = tvds.Locals[0];
                call_arguments = GetCallArguments(tvds.Update as UpdateStmt);

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
                        Util.Printer.Error(st, "Local variable {0} is not declared", ns.Name);
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
                Util.Printer.Error(st, "Wrong number of method result arguments; Expected {0} got {1}", 1, 0);

        }

        private Solution CallDefaultAction(Statement st)
        {
            Contract.Requires(st != null);
            
            Atomic state = this.Copy();
            state.AddUpdated(st, st);
            return new Solution(state, null);
        }

        /// <summary>
        /// Find closest while statement to the tactic call
        /// </summary>
        /// <param name="tac_stmt">Tactic call</param>
        /// <param name="member">Method</param>
        /// <returns>WhileStmt</returns>
        protected static WhileStmt FindWhileStmt(Statement tac_stmt, MemberDecl member)
        {
            Contract.Requires(tac_stmt != null);
            Contract.Requires(member != null);

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
            Contract.Requires(us != null);
            ExprRhs er = (ExprRhs)us.Rhss[0];
            return ((ApplySuffix)er.Expr).Args;
        }

        protected bool HasLocalWithName(NameSegment ns)
        {
            Contract.Requires<ArgumentNullException>(ns != null);
            return localContext.HasLocalWithName(ns);
        }

        protected object GetLocalValueByName(NameSegment ns)
        {
            Contract.Requires<ArgumentNullException>(ns != null);
            return localContext.GetLocalValueByName(ns.Name);
        }

        protected object GetLocalValueByName(IVariable variable)
        {
            Contract.Requires<ArgumentNullException>(variable != null);
            return localContext.GetLocalValueByName(variable);
        }

        protected object GetLocalValueByName(string name)
        {
            Contract.Requires<ArgumentNullException>(name != null);
            return localContext.GetLocalValueByName(name);
        }

        protected IVariable GetLocalKeyByName(NameSegment ns)
        {
            Contract.Requires<ArgumentNullException>(ns != null);
            return localContext.GetLocalKeyByName(ns.Name);
        }

        protected IVariable GetLocalKeyByName(string name)
        {
            Contract.Requires<ArgumentNullException>(name != null);
            return localContext.GetLocalKeyByName(name);
        }

        public void Fin()
        {
            globalContext.resolved.Clear();
            globalContext.resolved.AddRange(localContext.updated_statements.Values.ToArray());
            globalContext.new_target = localContext.new_target;
        }

        public void AddLocal(IVariable lv, object value)
        {
            Contract.Requires<ArgumentNullException>(lv != null);
            globalContext.program.IncTotalBranchCount(globalContext.program.currentDebug);
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
            globalContext.program.IncTotalBranchCount(globalContext.program.currentDebug);
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

        public List<Statement> GetAllUpdated()
        {
            Contract.Ensures(Contract.Result<List<Statement>>() != null);
            return localContext.GetAllUpdated();
        }

        public Dictionary<Statement, Statement> GetResult()
        {
            return localContext.updated_statements;
        }

        public bool IsFinal(List<Solution> solution_list)
        {
            Contract.Requires<ArgumentNullException>(solution_list != null);
            foreach (var item in solution_list)
            {
                if (!item.isFinal)
                    return false;
            }

            return true;
        }

        public Method GetNewTarget()
        {
            return localContext.new_target;
        }

        public void SetNewTarget(Method new_target)
        {
            localContext.new_target = new_target;
        }
        /// <summary>
        /// Creates a new tactic from a given tactic body and updates the context
        /// </summary>
        /// <param name="tac"></param>
        /// <param name="newBody"></param>
        /// <param name="decCounter"></param>
        /// <returns></returns>
        protected Solution CreateTactic(List<Statement> newBody, bool decCounter = true)
        {
            Contract.Ensures(Contract.Result<Solution>() != null);
            Tactic tac = localContext.tac;
            Tactic newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword,
                                        tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                                        tac.Decreases, new BlockStmt(tac.Body.Tok, tac.Body.EndTok, newBody),
                                        tac.Attributes, tac.SignatureEllipsis);
            Atomic newAtomic = this.Copy();
            newAtomic.localContext.tac = newTac;
            newAtomic.localContext.tac_body = newBody;
            /* HACK */
            // decrase the tactic body counter
            // so the interpreter would execute newly inserted atomic
            if (decCounter)
                newAtomic.localContext.DecCounter();
            return new Solution(newAtomic);
        }

        /// <summary>
        /// Replaces the statement at current body counter with a new statement
        /// </summary>
        /// <param name="oldBody"></param>
        /// <param name="newStatement"></param>
        /// <returns></returns>
        protected List<Statement> ReplaceCurrentAtomic(Statement newStatement)
        {
            Contract.Requires(newStatement != null);
            Contract.Ensures(Contract.Result<List<Statement>>() != null);
            int index = localContext.GetCounter();
            List<Statement> newBody = localContext.GetFreshTacticBody();
            newBody[index] = newStatement;
            return newBody;
        }

        protected List<Statement> ReplaceCurrentAtomic(List<Statement> list)
        {
            Contract.Requires(list != null);
            Contract.Ensures(Contract.Result<List<Statement>>() != null);
            int index = localContext.GetCounter();
            List<Statement> newBody = localContext.GetFreshTacticBody();
            newBody.RemoveAt(index);
            newBody.InsertRange(index, list);
            return newBody;
        }

        protected Expression EvaluateExpression(ExpressionTree expt)
        {
            Contract.Requires(expt != null);
            if (expt.isLeaf())
            {
                return EvaluateLeaf(expt) as Dafny.LiteralExpr;
            }
            else
            {
                Dafny.LiteralExpr lhs = EvaluateExpression(expt.lChild) as Dafny.LiteralExpr;
                Dafny.LiteralExpr rhs = EvaluateExpression(expt.rChild) as Dafny.LiteralExpr;

                // for now asume lhs and rhs are integers
                BigInteger l = (BigInteger)lhs.Value;
                BigInteger r = (BigInteger)rhs.Value;

                BigInteger res = 0;
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);

                switch (bexp.Op)
                {
                    case BinaryExpr.Opcode.Sub:
                        res = BigInteger.Subtract(l, r);
                        break;
                    case BinaryExpr.Opcode.Add:
                        res = BigInteger.Add(l, r);
                        break;
                    case BinaryExpr.Opcode.Mul:
                        res = BigInteger.Multiply(l, r);
                        break;
                    case BinaryExpr.Opcode.Div:
                        res = BigInteger.Divide(l, r);
                        break;
                }

                return new Dafny.LiteralExpr(lhs.tok, res);


            }
        }

        /// <summary>
        /// Evaluate a leaf node
        /// TODO: support for call evaluation
        /// </summary>
        /// <param name="expt"></param>
        /// <returns></returns>
        protected object EvaluateLeaf(ExpressionTree expt)
        {
            Contract.Requires(expt != null && expt.isLeaf());
            if (expt.data is NameSegment || expt.data is ApplySuffix)
            {
                // fix me
                foreach (var item in ProcessStmtArgument(expt.data))
                    return item;
            }
            else if (expt.data is Dafny.LiteralExpr)
                return expt.data;

            return null;
        }

        /// <summary>
        /// Resolve all variables in expression to either literal values
        /// or to orignal declared nameSegments
        /// </summary>
        /// <param name="guard"></param>
        /// <returns></returns>
        protected void ResolveExpression(ExpressionTree guard)
        {
            Contract.Requires(guard != null);
            if (guard.isLeaf())
            {
                Expression newNs; // potential encapsulation problems
                var result = EvaluateLeaf(guard);
                // we only need to replace nameSegments
                if (guard.data is NameSegment)
                {
                    
                    Contract.Assert(result != null);
                    if (result is Dafny.Formal)
                    {
                        var tmp = result as Dafny.Formal;
                        newNs = new NameSegment(tmp.tok, tmp.Name, null);
                    }
                    else if (result is NameSegment)
                    {
                        newNs = result as NameSegment;
                    }
                    else
                    {
                        newNs = result as Dafny.LiteralExpr;
                    }
                    guard.data = newNs;
                }

            }
            else
            {
                ResolveExpression(guard.lChild);
                if (guard.rChild != null)
                    ResolveExpression(guard.rChild);
            }
        }
        /// <summary>
        /// Determine whehter the given expression tree can be evaluated.
        /// THe value is true if all leaf nodes have literal values
        /// </summary>
        /// <param name="expt"></param>
        /// <returns></returns>
        [Pure]
        protected bool IsResolvable(ExpressionTree expt)
        {
            Contract.Requires(expt.isRoot());
            List<Expression> leafs = expt.GetLeafs();

            foreach (var leaf in leafs)
            {
                if (leaf is NameSegment)
                {
                    NameSegment ns = leaf as NameSegment;
                    object local = GetLocalValueByName(ns);
                    if (!(local is Dafny.LiteralExpr))
                        return false;
                }
                else if (leaf is Dafny.LiteralExpr || leaf is ApplySuffix)
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Generate a Dafny program and verify it
        /// </summary>
        protected bool GenerateAndVerify(Solution solution)
        {
            Contract.Requires<ArgumentNullException>(solution != null);
            Dafny.Program prog = globalContext.program.ParseProgram();
            solution.GenerateProgram(ref prog);
            globalContext.program.ClearBody(localContext.md);
            //globalContext.program.MaybePrintProgram(prog, null);
            if (!globalContext.program.ResolveProgram())
                return false;
            globalContext.program.VerifyProgram();
            return true;
        }
    }
}