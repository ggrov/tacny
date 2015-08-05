﻿using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    public interface AtomicStmt
    {
        string Resolve(Statement st, ref List<Solution> solution_list);
    }

    public class Action
    {

        public readonly MemberDecl md = null; // the Class Member from which the tactic has been called
        public readonly Tactic tac = null;  // The called tactic
        public List<Statement> tac_body = new List<Statement>(); // body of the currently worked tactic
        public readonly UpdateStmt tac_call = null;  // call to the tactic
        public Solution solution;
        public Program program;

        protected readonly GlobalContext globalContext;
        protected LocalContext localContext;

        protected Action(Action ac)
        {
            this.md = ac.md;
            this.tac = ac.tac;
            this.tac_body = ac.tac_body;
            this.tac_call = ac.tac_call;

            this.localContext = ac.localContext;
            this.globalContext = ac.globalContext;
            this.program = ac.globalContext.program;
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
            this.localContext = new LocalContext(md, tac, tac_call);
            this.globalContext = new GlobalContext(md, tac_call, program);
            FillTacticInputs();
        }

        private Action(MemberDecl md, Tactic tac, List<Statement> tac_body, UpdateStmt tac_call,
                       LocalContext localContext, GlobalContext globalContext)
        {
            this.md = md;
            this.tac = tac;
            this.tac_call = tac_call;
            this.tac_body = new List<Statement>(tac_body.ToArray());
            this.program = globalContext.program.NewProgram();
            this.globalContext = globalContext;
            this.localContext = localContext.Copy();
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
                    tac_body, tac_call, localContext, globalContext);
        }

        public virtual string FormatError(string err)
        {
            return "Error: " + err;
        }

        public static string ResolveTactic(Tactic tac, UpdateStmt tac_call, MemberDecl md, Program tacnyProgram, out SolutionList result)
        {
            result = new SolutionList();
            List<Solution> res = result.plist;
            string err = ResolveTactic(tac, tac_call, md, tacnyProgram, ref res);
            result.plist = res;
            result.AddFinal(res);
            return err;

        }
        public static string ResolveTactic(Tactic tac, UpdateStmt tac_call, MemberDecl md, Program tacnyProgram, ref List<Solution> result)
        {
            Contract.Requires(tac != null);
            Contract.Requires(tac_call != null);
            Contract.Requires(md != null);
            result = null;
            //local solution list
            SolutionList solution_list = new SolutionList(new Solution(new Action(md, tac, tac_call, tacnyProgram)));
            string err = null;

            while (!solution_list.IsFinal())
            {
                List<Solution> res = null;

                err = Action.ResolveOne(ref res, solution_list.plist);
                if (err != null)
                    return err;

                if (res.Count > 0)
                    solution_list.AddRange(res);
                else
                    break;
            }

            result = solution_list.plist;
            return null;
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

        protected string ResolveBlockStmt(BlockStmt bs, out List<Statement> stmt_list)
        {
            Contract.Requires(bs != null);
            string err;
            stmt_list = new List<Statement>();
            List<Solution> solution_list = new List<Solution>();
            Action ac = new Action(md, tac, tac_call, program);

            ac.tac_body = bs.Body;

            foreach (var stmt in ac.tac_body)
            {
                err = ac.CallAction(stmt, ref solution_list);
                if (err != null)
                    return err;

                foreach (var res in solution_list)
                    if (res.parent == null)
                        res.parent = solution;

            }

            foreach (var solution in solution_list)
            {
                solution.state.Fin();
                stmt_list.AddRange(solution.state.GetResolved());
            }
            return null;
        }

        public string CallAction(object call, ref List<Solution> solution_list)
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
                    type = StatementRegister.GetStatementType(StatementRegister.GetAtomicType(aps.Lhs.tok.val));
                else
                    return "unexpected call argument: expectet Statement or ApplySuffix; Received " + call.GetType();
            }
            if (type == null)
            {
                UpdateStmt us = st as UpdateStmt;
                if (us != null)
                {
                    if (program.IsTacticCall(us))
                    {
                        return Action.ResolveTactic(program.GetTactic(us), us, tac, program, ref solution_list);
                    }
                }
                return CallDefaultAction(st, ref solution_list);
            }
                
            var qq = Activator.CreateInstance(type, new object[] { this }) as AtomicStmt;

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
                result = localContext.local_variables[lv];
                localContext.local_variables.Remove(lv);

            }
            else
                result = argument;

            return null;
        }

        private string CallDefaultAction(Statement st, ref List<Solution> solution_list)
        {
            Action state = this.Copy();
            state.localContext.updated_statements.Add(st, st);
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
            localContext.Fin();
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
            return localContext.resolved;
        }

        /*
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
         *      private string CallSingletonAction(Statement st, ref List<Solution> solution_list)
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
*/
    }
}


