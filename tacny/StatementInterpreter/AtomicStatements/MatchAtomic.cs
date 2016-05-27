using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Util;
using Formal = Microsoft.Dafny.Formal;

// todo cases multiple tac calls, cases within cases, tac calls from cases etc.
// update 

namespace Tacny
{
    class MatchAtomic : Atomic, IAtomicStmt
    {

        private Token oldToken;

        public MatchAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            GenerateMatch(st as TacnyCasesBlockStmt, ref solution_list);
        }

        /*
         * A matchStatement error token will always be a case tok 
         * we find the first failing cases block and return the index 
         */
        public int GetErrorIndex(Token errorToken, MatchStmt st)
        {
            foreach (var item in st.Cases)
                if (item.tok == errorToken)
                    return st.Cases.IndexOf(item);

            return -1;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorToken"></param>
        /// <param name="ms"></param>
        /// <param name="ctor"></param>
        /// <returns></returns>
        public bool ErrorChanged(Token errorToken, MatchStmt ms, int ctor)
        {
            // check if error has been generated
            if (oldToken == null || errorToken == null)
                return true;

            /**
             * Check if the error originates in the current cases statement
             */
            if (oldToken.line <= ms.Cases[ctor].tok.line + ms.Cases[ctor].Body.Count || errorToken.line == ms.Cases[ctor].tok.line)
            {
                // if the error occurs in the last cases element
                if (ctor + 1 == ms.Cases.Count)
                {
                    // check if the error resides anywhere in the last case body
                    if (errorToken.line > oldToken.line && errorToken.line <= (ms.Cases[ctor].tok.line + ms.Cases[ctor].Body.Count))
                        return true;
                    return false;
                }
              if (errorToken.line > oldToken.line && errorToken.line <= ms.Cases[ctor + 1].tok.line)
                return true;
              return false;
            }
            // the error must have changed
            return true;
        }

        private void GenerateMatch(TacnyCasesBlockStmt st, ref List<Solution> solution_list)
        {
            DatatypeDecl datatype = null;
            Solution solution = null;
            List<Solution> result = null;
            MatchStmt ms = null;
            ParensExpression guard = null;
            NameSegment guard_arg = null;
            string datatype_name = null;
            List<List<Solution>> allCtorBodies = null;
            bool isElement = false;

            bool[] ctorFlags = null; //localContext.ctorFlags; // used to keep track of which cases statements require a body
            int ctor = 0; // current active match case
            List<Solution> ctorBodies = null;
            guard = st.Guard as ParensExpression;

            if (guard == null)
                guard_arg = st.Guard as NameSegment;
            else
                guard_arg = guard.E as NameSegment;

            Contract.Assert(guard_arg != null, Error.MkErr(st, 2));

            IVariable tac_input = GetLocalKeyByName(guard_arg);
            Contract.Assert(tac_input != null, Error.MkErr(st, 9, guard_arg.Name));


            if (!(tac_input is Formal))
            {
                tac_input = GetLocalValueByName(guard_arg) as IVariable;
                Contract.Assert(tac_input != null, Error.MkErr(st, 9, guard_arg.Name));
                // the original
                guard_arg = new NameSegment(tac_input.Tok, tac_input.Name, null);
            }
            else
            {
                // get the original declaration inside the method
                guard_arg = GetLocalValueByName(tac_input) as NameSegment;
            }
            datatype_name = tac_input.Type.ToString();
            /**
             * TODO cleanup
             * if datatype is Element lookup the formal in global variable registry
             */

            if (datatype_name == "Element")
            {
                isElement = true;
                object val = GetLocalValueByName(tac_input.Name);
                NameSegment decl = val as NameSegment;
                Contract.Assert(decl != null, Error.MkErr(st, 9, tac_input.Name));

                IVariable original_decl = globalContext.GetGlobalVariable(decl.Name);
                if (original_decl != null)
                {
                    UserDefinedType udt = original_decl.Type as UserDefinedType;
                    if (udt != null)
                        datatype_name = udt.Name;
                    else
                        datatype_name = original_decl.Type.ToString();
                }
                else
                    Contract.Assert(false, Error.MkErr(st, 9, tac_input.Name));
            }
            //else
            //  Contract.Assert(false, Util.Error.MkErr(st, 1, "Element"));


            if (!globalContext.ContainsGlobalKey(datatype_name))
            {
                Contract.Assert(false, Error.MkErr(st, 12, datatype_name));
            }

            datatype = globalContext.GetGlobal(datatype_name);
            if (isElement)
            {
                InitCtorFlags(datatype, out ctorFlags);
                ctorBodies = RepeatedDefault<Solution>(datatype.Ctors.Count);
                // find the first failing case 
                GenerateMatchStmt(localContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(guard_arg), datatype, ctorBodies, out ms, ctorFlags);
                Atomic ac = Copy();
                ac.AddUpdated(ms, ms);
                solution = new Solution(ac, true, null);
                if (!GenerateAndVerify(solution))
                    ctor = 0;
                else
                {
                    ctor = GetErrorIndex(globalContext.program.GetErrorToken(), ms);
                    ctorFlags[ctor] = true;
                    oldToken = globalContext.program.GetErrorToken();
                }
            }
            else
            {
                allCtorBodies = new List<List<Solution>>();
                ctor = 0;
            }
            while (true)
            {
                if (ctor >= datatype.Ctors.Count || !globalContext.program.HasError())
                    break;

                RegisterLocals(datatype, ctor);
                ResolveBody(st.Body, out result);
                // if nothing was generated for the cases body move on to the next one
                if (!isElement)
                {
                    result.Insert(0, null);
                    allCtorBodies.Add(result);
                    result = new List<Solution>();
                    ctor++;
                    continue;
                }
                if (result.Count == 0)
                    ctor++;
                else {
                    for (int i = 0; i < result.Count; i++)
                    {
                        ctorBodies[ctor] = result[i];
                        GenerateMatchStmt(localContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(guard_arg), datatype, ctorBodies, out ms, ctorFlags);
                        Atomic ac = Copy();
                        ac.AddUpdated(ms, ms);
                        solution = new Solution(ac, true, null);
                        if (!GenerateAndVerify(solution))
                            continue;
                        if (!globalContext.program.HasError())
                            break;
                        // TODO: if error is: "could not prove termination", skip solution
                        if (CheckError(ms, ref ctorFlags, ctor))
                        {
                            int index = GetErrorIndex(oldToken, ms);
                            result = new List<Solution>();
                            // if the ctor does not require a body null the value
                            if (!ctorFlags[ctor])
                                ctorBodies[ctor] = null;
                            RemoveLocals(datatype, ctor);
                            ctor++;
                            break;
                        }
                    }
                }
            }

            /*
             * HACK Recreate the match block as the old one was modified by the resolver
             */
            if (isElement)
            {
                GenerateMatchStmt(localContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(guard_arg), datatype, ctorBodies, out ms, ctorFlags);
                AddUpdated(ms, ms);
                solution_list.Add(new Solution(Copy(), true, null));
            }
            else
            {
                List<MatchStmt> cases = new List<MatchStmt>();
                GenerateAllMatchStmt(localContext.tac_call.Tok.line, 0, Util.Copy.CopyNameSegment(guard_arg), datatype, allCtorBodies, new List<Solution>(), ref cases);
                foreach (var item in cases)
                {
                    Atomic ac = Copy();
                    ac.AddUpdated(item, item);
                    solution_list.Add(new Solution(ac, true, null));
                }
            }

        }

        private void GenerateMatchStmt(int index, NameSegment ns, DatatypeDecl datatype, List<Solution> body, out MatchStmt result, bool[] flags)
        {
            Contract.Requires(ns != null);
            Contract.Requires(datatype != null);
            Contract.Ensures(Contract.ValueAtReturn(out result) != null);
            List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
            result = null;
            int line = index + 1;
            int i = 0;
            foreach (DatatypeCtor dc in datatype.Ctors)
            {
                MatchCaseStmt mcs;
                GenerateMatchCaseStmt(line, dc, body[i], out  mcs);

                cases.Add(mcs);
                line += mcs.Body.Count + 1;
                i++;
            }

            result = new MatchStmt(CreateToken("match", index, 0), CreateToken("=>", index, 0), ns, cases, false);
        }

        private void GenerateAllMatchStmt(int line_index, int depth, NameSegment ns, DatatypeDecl datatype, List<List<Solution>> bodies, List<Solution> curBody, ref List<MatchStmt> result)
        {
            if (bodies.Count == 0) return;
            if (depth == bodies.Count)
            {
                MatchStmt ms;
                bool [] flags;
                InitCtorFlags(datatype, out flags, true);
                GenerateMatchStmt(line_index, Util.Copy.CopyNameSegment(ns), datatype, curBody, out ms, flags);
                result.Add(ms);
                return;
            }
            for (int i = 0; i < bodies[depth].Count; ++i)
            {
                List<Solution> tmp = new List<Solution>();
                tmp.AddRange(curBody);
                tmp.Add(bodies[depth][i]);
                GenerateAllMatchStmt(line_index, depth + 1, ns, datatype, bodies, tmp, ref result);
            }
        }

        private void GenerateMatchCaseStmt(int line, DatatypeCtor dtc, Solution solution, out MatchCaseStmt mcs)
        {
            Contract.Requires(dtc != null);
            Contract.Ensures(Contract.ValueAtReturn(out mcs) != null);
            List<CasePattern> casePatterns = new List<CasePattern>();
            mcs = null;
            dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);
            foreach (Formal formal in dtc.Formals)
            {
                CasePattern cp;
                GenerateCasePattern(line, formal, out cp);
                casePatterns.Add(cp);
            }

            List<Statement> body = new List<Statement>();
            if (solution != null)
            {
                Atomic ac = solution.state.Copy();
                body = ac.GetAllUpdated();
            }
            mcs = new MatchCaseStmt(CreateToken("cases", line, 0), dtc.CompileName, casePatterns, body);
        }

        private void GenerateCasePattern(int line, Formal formal, out CasePattern cp)
        {
            Contract.Requires(formal != null);
            formal = new Formal(formal.tok, formal.Name, formal.Type, formal.InParam, formal.IsGhost);

            cp = new CasePattern(CreateToken(formal.Name, line, 0),
                                    new BoundVar(CreateToken(formal.Name, line, 0), formal.Name, new InferredTypeProxy()));
        }

        private static void InitCtorFlags(DatatypeDecl datatype, out bool[] flags, bool value = false)
        {
            flags = new bool[datatype.Ctors.Count];
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = value;
            }
        }

        private static void InitSolFlags(bool[] flags)
        {
            for (int i = 0; i < flags.Length; i++)
                flags[i] = true;
        }

        private static bool ValidateSolFlags(bool[] flags)
        {
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                    return true;
            }
            return false;
        }

        private void RegisterLocals(DatatypeDecl datatype, int index)
        {
            foreach (var formal in datatype.Ctors[index].Formals)
            {
                // register globals as name segments
                globalContext.RegsiterGlobalVariable(formal);
            }
        }

        private void RemoveLocals(DatatypeDecl datatype, int index)
        {
            foreach (var formal in datatype.Ctors[index].Formals)
            {
                // register globals as name segments
                globalContext.RemoveGlobalVariable(formal);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool CheckError(MatchStmt ms, ref bool[] ctorFlags, int ctor)
        {
            // hack for termination
            if (globalContext.program.ErrorInfo.Msg == "cannot prove termination; try supplying a decreases clause")
                return false;
            // if the error token has not changed since last iteration
            if (!ErrorChanged(globalContext.program.GetErrorToken(), ms, ctor))
                return false;

            oldToken = globalContext.program.GetErrorToken();
            if (oldToken != null)
            {
                int index = GetErrorIndex(oldToken, ms);
                // the verification error is not caused by the match stmt
                if (index == -1)
                    return false;
                ctorFlags[index] = true;
                return true;
            }
            return false;
        }

        public static List<T> RepeatedDefault<T>(int count)
        {
            return Repeated(default(T), count);
        }


        public static List<T> Repeated<T>(T value, int count)
        {
            List<T> ret = new List<T>(count);
            ret.AddRange(Enumerable.Repeat(value, count));
            return ret;
        }
    }
}
