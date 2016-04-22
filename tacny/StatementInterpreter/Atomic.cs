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

namespace Tacny
{
    [ContractClass(typeof(AtomicContract))]
    public interface IAtomicStmt
    {
        void Resolve(Statement st, ref List<Solution> solution_list);
    }

    [ContractClassFor(typeof(IAtomicStmt))]
    // Validate the input before execution
    public abstract class AtomicContract : IAtomicStmt
    {

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires<ArgumentNullException>(st != null);
            Contract.Requires(tcce.NonNullElements<Solution>(solution_list));
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

        public Atomic(MemberDecl md, Tactic tac, UpdateStmt tac_call, Program program)
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


        public static void ResolveTactic(Tactic tac, UpdateStmt tac_call, MemberDecl md, Program tacnyProgram, List<IVariable> variables, List<IVariable> resolved, ref SolutionList result)
        {
            Contract.Requires(tac != null);
            Contract.Requires(tac_call != null);
            Contract.Requires(md != null);
            Contract.Requires(tacnyProgram != null);
            Contract.Requires(tcce.NonNullElements<IVariable>(variables));
            Contract.Requires(tcce.NonNullElements<IVariable>(resolved));
            Contract.Requires(result != null);
            List<Solution> res = null;

            if (result.plist.Count == 0)
            {
                res = new List<Solution>();
                Atomic ac = new Atomic(md, tac, tac_call, tacnyProgram);
                ac.globalContext.RegsiterGlobalVariables(variables, resolved);
                ResolveTactic(ref res, ac);
            }
            else
            {
                res = new List<Solution>();
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
                ResolveTactic(ref res);
            }


            result.AddRange(res);
        }

        /// <summary>
        /// Resolve tactic body, given that no tactic calls have been made before
        /// </summary>
        /// <param name="atomic">The base atomic class</param>
        /// <param name="result">Result list</param>
        /// <returns>Error message</returns>
        public static void ResolveTactic(ref List<Solution> result, Atomic atomic = null)
        {
            Contract.Requires(result != null);
            //local solution list
            SolutionList solution_list = atomic == null ? new SolutionList() : new SolutionList(new Solution(atomic));
            // if previous solutions exist, add them 
            if (result.Count > 0)
                solution_list.AddRange(result);

            while (true)
            {
                List<Solution> res = null;

                ResolveStatement(ref res, solution_list.plist);

                if (res.Count > 0)
                    solution_list.AddRange(res);
                else
                    break;
            }

            result.AddRange(solution_list.plist);
        }

        public static void ResolveStatement(ref List<Solution> result, List<Solution> solution_list)
        {
            Contract.Requires(solution_list != null);

            if (result == null)
                result = new List<Solution>();
            if (Util.TacnyOptions.O.ParallelExecution)
            {
                List<Solution> temp = result;
                Parallel.ForEach(solution_list, (solution =>
                {
                    if (solution.state.localContext.IsResolved())
                        return;
                    // if no statements have been resolved, check the preconditions
                    if (solution.state.localContext.IsFirstStatment())
                        TacnyContract.ValidateRequires(solution);
                    solution.state.CallAction(solution.state.localContext.GetCurrentStatement(), ref temp);

                    if (solution_list.IndexOf(solution) == solution_list.Count - 1)
                    {
                        foreach (var res in temp)
                        {
                            if (res.parent == null)
                            {
                                res.parent = solution;
                                res.state.localContext.IncCounter();
                            }
                        }
                    }
                }));
                result = temp;
            }
            else
            {
                foreach (var solution in solution_list)
                {
                    if (solution.state.localContext.IsResolved())
                        continue;
                    // if no statements have been resolved, check the preconditions
                    if (solution.state.localContext.IsFirstStatment())
                        TacnyContract.ValidateRequires(solution);
                    solution.state.CallAction(solution.state.localContext.GetCurrentStatement(), ref result);

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
            }
        }

    
        /// <summary>
        /// Resolves atomic statements inside a block stmt
        /// </summary>
        /// <param name="body"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        protected void ResolveBody(BlockStmt body, out List<Solution> result)
        {
            Contract.Requires<ArgumentNullException>(body != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            //result = null;
            Atomic atomic = this.Copy();
            atomic.localContext.tacticBody = body.Body;
            atomic.localContext.ResetCounter();
            if (result == null || result.Count == 0)
                result = new List<Solution>() { new Solution(atomic) };
            while (true)
            {

                List<Solution> res = new List<Solution>();
                foreach (var solution in result)
                {
                    Statement nextStmt = solution.state.localContext.GetCurrentStatement();
                    if (nextStmt == null)
                    {
                        // if all the statements have been analysed finalise the solution and skip to th next
                        solution.isFinal = true;
                        res.Add(solution);
                        continue;
                    }
                    solution.state.CallAction(nextStmt, ref res);
                }

                // check if all solutions are final
                if (IsFinal(res))
                {
                    result.Clear();
                    result.AddRange(res);
                    break;
                }

                // increment program counter
                foreach (var sol in res)
                {
                    if (!sol.isFinal)
                        sol.state.localContext.IncCounter();
                }

                result.Clear();
                result.AddRange(res);
            }

        }

        protected void CallAction(object call, ref List<Solution> solution_list)
        {
            System.Type type = null;
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
                    st = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                }
            }
            if (type == null)
            {
                UpdateStmt us = st as UpdateStmt;

                if (us != null)
                {
                    // if the statement is nested tactic call
                    if (globalContext.program.IsTacticCall(us))
                    {
                        Atomic ac;
                        Tactic tac = globalContext.program.GetTactic(us);
                        ac = new Atomic(localContext.md, tac, us, globalContext);

                        ExprRhs er = (ExprRhs)ac.localContext.tac_call.Rhss[0];
                        List<Expression> exps = ((ApplySuffix)er.Expr).Args;
                        Contract.Assert(exps.Count == ac.localContext.tactic.Ins.Count);
                        ac.SetNewTarget(GetNewTarget());
                        for (int i = 0; i < exps.Count; i++)
                        {
                            Expression result = null;
                            ProcessArg(exps[i], out result);

                            ac.AddLocal(ac.localContext.tactic.Ins[i], result);
                        }
                        List<Solution> sol_list = new List<Solution>();
                        ResolveTactic(ref sol_list, ac);

                        /**
                         * Transfer the results from evaluating the nested tactic
                         */
                        foreach (var solution in sol_list)
                        {
                            Atomic action = this.Copy();
                            action.SetNewTarget(solution.state.GetNewTarget());
                            foreach (KeyValuePair<Statement, Statement> kvp in solution.state.GetResult())
                                action.AddUpdated(kvp.Key, kvp.Value);
                            solution_list.Add(new Solution(action));
                        }
                    }
                }


                var vds = st as TacticVarDeclStmt;
                // if empty variable declaration
                // register variable to local with empty value
                if (vds != null)
                {
                    Solution sol;
                    RegisterLocalVariable(vds, out sol);
                    solution_list.Add(sol);
                }
                else
                    CallDefaultAction(st, ref solution_list);
            }
            else
            {
                var qq = Activator.CreateInstance(type, new object[] { this }) as IAtomicStmt;
                if (qq == null)
                    CallDefaultAction(st, ref solution_list);
                else
                    qq.Resolve(st, ref solution_list);
            }
        }

        private void RegisterLocalVariable(TacticVarDeclStmt declaration, out Solution result)
        {
            Contract.Requires(declaration != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            result = null;
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
                        Contract.Assert(index >= 0);
                        if (declaration.Locals.Count < index)
                        {
                            Util.Printer.Error(declaration, "Not all declared variables have an assigned value");
                            return;
                        }
                        ExprRhs exprRhs = item as ExprRhs;
                        // if the declaration is literal expr (e.g. var q := 1)
                        Dafny.LiteralExpr litExpr = exprRhs.Expr as Dafny.LiteralExpr;
                        if (litExpr != null)
                            AddLocal(declaration.Locals[index], litExpr);
                        else
                        {
                            Expression res;
                            ProcessArg(exprRhs.Expr, out res);
                            AddLocal(declaration.Locals[index], res);
                        }
                    }
                }
            }
            else
            {
                foreach (var item in declaration.Locals)
                    AddLocal(item as IVariable, null);
            }
            //AddLocal(declaration.Locals[0], val);
            result = new Solution(this.Copy());
        }

        /**
         * !!!TODO: HOW AM I REQUIRED!!!
         * 
         * 
         */
        private void RegisterLocalVariable(UpdateStmt updateStmt, out Solution result)
        {
            Contract.Requires(updateStmt != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            result = null;
            foreach (var item in updateStmt.Rhss)
            {
                int index = updateStmt.Rhss.IndexOf(item);
                if (updateStmt.Lhss.Count < index)
                {
                    Util.Printer.Error(updateStmt, "Not all variables have an assigned value");
                    return;
                }
                // check if lhs is declared
                NameSegment lhs = updateStmt.Lhss[index] as NameSegment;
                if (lhs == null)
                {
                    Util.Printer.Error(updateStmt, "Unexpected declaration type, expected NameSegment, received {0}", updateStmt.Lhss[index].GetType());
                    return;
                }
                IVariable local = GetLocalKeyByName(lhs);
                if (local == null)
                {
                    Util.Printer.Error(updateStmt, "Local variable {0} is not declared", lhs.Name);
                    return;
                }
                ExprRhs exprRhs = item as ExprRhs;
                // if the declaration is literal expr (e.g. var q := 1)
                Dafny.LiteralExpr litExpr = exprRhs.Expr as Dafny.LiteralExpr;
                if (litExpr != null)
                    AddLocal(local, litExpr);
                else
                {
                    Expression res;
                    ProcessArg(exprRhs.Expr, out res);
                    AddLocal(local, res);
                }
            }

            result = new Solution(this.Copy());
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

        public void ProcessArg(Expression argument, out Expression result)
        {
            Contract.Requires<ArgumentNullException>(argument != null);
            Contract.Ensures(Contract.ValueAtReturn<Expression>(out result) != null);
            object tmp;
            ProcessArg(argument, out tmp);
            result = (Expression)tmp;
        }

        public void ProcessArg(Expression argument, out object result)
        {
            Contract.Requires<ArgumentNullException>(argument != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            result = null;
            NameSegment nameSegment = null;
            ApplySuffix aps = null;
            if ((nameSegment = argument as NameSegment) != null)
            {
                if (!HasLocalWithName(nameSegment))
                    Util.Printer.Error(argument, "Argument {0} not passed", nameSegment.Name);
                result = GetLocalValueByName(nameSegment.Name);
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
                CallAction(tvds, ref sol_list); // change

                result = localContext.localDeclarations[lv];
                localContext.localDeclarations.Remove(lv);

            }
            else if (argument is BinaryExpr || argument is ParensExpression)
            {
                ExpressionTree expt = ExpressionTree.ExpressionToTree(argument);
                ResolveExpression(expt);
                if (IsResolvable(expt))
                    result = EvaluateExpression(expt);
                else
                    result = expt.TreeToExpression();
            }
            else
                result = argument;
        }

        /// <summary>
        /// Called when the statement is not atomic to insert the statment as Dafny code
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private void CallDefaultAction(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            Contract.Requires(solution_list != null);

            /*
             * If the statement is updateStmt check for variable assignment 
             */
            if (st is UpdateStmt)
            {
                UpdateStmt us = st as UpdateStmt;
                Solution sol;
                // if localc have been succesffuly registered
                RegisterLocalVariable(us, out sol);
                Contract.Assert(sol != null);
                solution_list.Add(sol);
                return;

            }
            Atomic state = this.Copy();
            state.AddUpdated(st, st);
            solution_list.Add(new Solution(state, null));
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
            globalContext.resolved.AddRange(localContext.generatedStatements.Values.ToArray());
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
            return localContext.generatedStatements;
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
            Tactic tac = localContext.tactic;
            Tactic newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword,
                                        tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                                        tac.Decreases, new BlockStmt(tac.Body.Tok, tac.Body.EndTok, newBody),
                                        tac.Attributes, tac.SignatureEllipsis);
            Atomic newAtomic = this.Copy();
            newAtomic.localContext.tactic = newTac;
            newAtomic.localContext.tacticBody = newBody;
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
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
                if (BinaryExpr.IsEqualityOp(bexp.Op))
                {
                    var boolVal = EvaluateEqualityExpression(expt);
                    return new Dafny.LiteralExpr(new Token(), boolVal);
                }
                else {
                    Dafny.LiteralExpr lhs = EvaluateExpression(expt.lChild) as Dafny.LiteralExpr;
                    Dafny.LiteralExpr rhs = EvaluateExpression(expt.rChild) as Dafny.LiteralExpr;
                    // for now asume lhs and rhs are integers
                    BigInteger l = (BigInteger)lhs.Value;
                    BigInteger r = (BigInteger)rhs.Value;

                    BigInteger res = 0;


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
        }

        /// <summary>
        /// Evalutate an expression tree
        /// </summary>
        /// <param name="expt"></param>
        /// <returns></returns>
        public bool EvaluateEqualityExpression(ExpressionTree expt)
        {
            Contract.Requires(expt != null);
            // if the node is leaf, cast it to bool and return
            if (expt.isLeaf())
            {
                Dafny.LiteralExpr lit = EvaluateLeaf(expt) as Dafny.LiteralExpr;
                return lit.Value is bool ? (bool)lit.Value : false;
            }
            // left branch only
            else if (expt.lChild != null && expt.rChild == null)
                return EvaluateEqualityExpression(expt.lChild);
            // if there is no more nesting resolve the expression
            else if (expt.lChild.isLeaf() && expt.rChild.isLeaf())
            {
                Dafny.LiteralExpr lhs = null;
                Dafny.LiteralExpr rhs = null;
                lhs = EvaluateLeaf(expt.lChild) as Dafny.LiteralExpr;
                rhs = EvaluateLeaf(expt.rChild) as Dafny.LiteralExpr;
                if (!lhs.GetType().Equals(rhs.GetType()))
                    return false;
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
                int res = -1;
                if (lhs.Value is BigInteger)
                {
                    BigInteger l = (BigInteger)lhs.Value;
                    BigInteger r = (BigInteger)rhs.Value;
                    res = l.CompareTo(r);
                }
                else if (lhs.Value is string)
                {
                    string l = lhs.Value as string;
                    string r = rhs.Value as string;
                    res = l.CompareTo(r);
                }
                else if (lhs.Value is bool)
                {
                    res = ((bool)lhs.Value).CompareTo((bool)rhs.Value);
                }

                if (bexp.Op == BinaryExpr.Opcode.Eq)
                    return res == 0;
                else if (bexp.Op == BinaryExpr.Opcode.Neq)
                    return res != 0;
                else if (bexp.Op == BinaryExpr.Opcode.Ge)
                    return res >= 0;
                else if (bexp.Op == BinaryExpr.Opcode.Gt)
                    return res > 0;
                else if (bexp.Op == BinaryExpr.Opcode.Le)
                    return res <= 0;
                else if (bexp.Op == BinaryExpr.Opcode.Lt)
                    return res < 0;
            }
            else // evaluate a nested expression
            {
                BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
                if (bexp.Op == BinaryExpr.Opcode.And)
                    return EvaluateEqualityExpression(expt.lChild) && EvaluateEqualityExpression(expt.rChild);
                else if (bexp.Op == BinaryExpr.Opcode.Or)
                    return EvaluateEqualityExpression(expt.lChild) || EvaluateEqualityExpression(expt.rChild);
            }
            return false;
        }

        /// <summary>
        /// Evaluate a leaf node
        /// TODO: support for call evaluation
        /// </summary>
        /// <param name="expt"></param>
        /// <returns></returns>
        protected Expression EvaluateLeaf(ExpressionTree expt)
        {
            Contract.Requires(expt != null && expt.isLeaf());
            if (expt.data is NameSegment || expt.data is ApplySuffix)
            {
                Expression result = null;
                ProcessArg(expt.data, out result);
                Contract.Assert(result != null);
                return result;
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
                // we only need to replace nameSegments
                if (guard.data is NameSegment)
                {
                    Expression newNs; // potential encapsulation problems
                    object result;
                    ProcessArg(guard.data, out result);
                    Contract.Assert(result != null);
                    if (result is MemberDecl)
                    {
                        MemberDecl md = result as MemberDecl;
                        newNs = new Dafny.StringLiteralExpr(new Token(), md.Name, true);
                    }
                    else if (result is Dafny.Formal)
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
            globalContext.program.PrintMember(prog, solution.state.localContext.md.Name);
            if (!globalContext.program.ResolveProgram())
                return false;
            globalContext.program.VerifyProgram();
            return true;
        }
    }
}