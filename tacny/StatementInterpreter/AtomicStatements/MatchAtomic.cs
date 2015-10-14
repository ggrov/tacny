﻿using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System;
using System.Threading;
// todo cases multiple tac calls, cases within cases, tac calls from cases etc.
// update 

namespace Tacny
{
    class MatchAtomic : Atomic, IAtomicStmt
    {

        private Token oldToken = null;

        public override string FormatError(string error)
        {
            return "ERROR cases: " + error;
        }

        public MatchAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            string err = GenerateMatch(st as TacnyCasesBlockStmt, ref solution_list);
            return err;
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
             * 
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
                else
                {
                    if (errorToken.line > oldToken.line && errorToken.line <= ms.Cases[ctor + 1].tok.line)
                        return true;
                    return false;
                }
            }
            // the error must have changed
            return true;
        }

        public string GenerateMatch(TacnyCasesBlockStmt st, ref List<Solution> solution_list)
        {
            DatatypeDecl datatype = null;
            Solution solution;
            Dafny.Program dprog;
            List<Solution> result = null;
            MatchStmt ms = null;
            ParensExpression guard;
            NameSegment ns;
            string datatype_name;
            string err;
            bool[] ctorFlags; //localContext.ctorFlags; // used to keep track of which cases statements require a body
            int ctor = 0; // current active match case
            List<Solution> ctor_bodies;
            guard = st.Guard as ParensExpression;

            if (guard == null)
                ns = st.Guard as NameSegment;
            else
                ns = guard.E as NameSegment;

            if (ns == null)
                return FormatError("unexpected cases argument");

            Dafny.Formal formal = (Dafny.Formal)GetLocalKeyByName(ns);
            if (formal == null)
                return FormatError("argument " + ns.Name + " is not declared");

            datatype_name = formal.Type.ToString();
            /**
             * TODO cleanup
             * if datatype is Element lookup the formal in global variable registry
             */

            if (datatype_name == "Element")
            {
                object val = localContext.GetLocalValueByName(formal.Name);
                NameSegment decl = val as NameSegment;
                if (decl == null)
                    return String.Format("cases: Argument {0} is not declared", formal.Name);
                IVariable original_decl = globalContext.GetGlobalVariable(decl.Name);
                if (original_decl != null)
                {
                    UserDefinedType udt = original_decl.Type as UserDefinedType;
                    if (udt != null)
                    {
                        datatype_name = udt.Name;
                    }
                    else
                        datatype_name = original_decl.Type.ToString();
                }
                else
                    return String.Format("cases: Argument {0} is not declared", formal.Name);

            }
            else
                return String.Format("cases: Argument {0} is undefined", formal.Name);


            if (!globalContext.ContainsGlobalKey(datatype_name))

                return String.Format("cases: datatype {0} is undefined", datatype_name);
            ns = localContext.GetLocalValueByName(formal) as NameSegment;

            datatype = globalContext.GetGlobal(datatype_name);
            InitCtorFlags(datatype, out ctorFlags);
            ctor_bodies = RepeatedDefault<Solution>(4);

            dprog = program.ParseProgram();
            while (true)
            {
                if (ctor >= datatype.Ctors.Count || !program.HasError())
                    break;
                RegisterLocals(datatype, ctor);
                err = ResolveBody(st.Body, out result);
                if (err != null)
                    return err;
                for (int i = 0; i < result.Count; i++)
                {
                    ctor_bodies[ctor] = result[i];
                    GenerateMatchStmt(st.Tok.line, new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, ctor_bodies, out ms, ctorFlags);
                    Atomic ac = this.Copy();
                    ac.AddUpdated(ms, ms);
                    solution = new Solution(ac, true, null);
                    dprog = program.ParseProgram();
                    solution.GenerateProgram(ref dprog);
                    program.ClearBody(localContext.md);
                    err = program.ResolveProgram();
                    // skip the solution if resolution failed    
                    if (err != null)
                    {
                        IncBadBranchCount();
                        continue;
                    }
                    //program.MaybePrintProgram(dprog, null);
                    program.VerifyProgram();
                    if (!program.HasError())
                        break;
                    // check if error index has changed
                    if (CheckError(ms, ref ctorFlags, ctor))
                    {

                        // if the ctor does not require a body null the value
                        if (!ctorFlags[ctor])
                            ctor_bodies[ctor] = null;
                        break;
                    }
                    IncBadBranchCount();
                }
                RemoveLocals(datatype, ctor);
                ctor++;
                //err = ResolveBody(st.Body, out result);


            }

            /*
             * HACK Recreate the match block as the old one was modified by the resolver
             */
            GenerateMatchStmt(st.Tok.line, new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, ctor_bodies, out ms, ctorFlags);
            AddUpdated(ms, ms);
            IncTotalBranchCount();

            solution_list.Add(new Solution(this.Copy(), true, null));
            return null;
        }

        private string GenerateMatchStmt(int index, NameSegment ns, DatatypeDecl datatype, List<Solution> body, out MatchStmt result, bool[] flags)
        {
            Contract.Requires(ns != null);
            Contract.Requires(datatype != null);
            string err;
            List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
            result = null;
            int line = index + 1;
            int i = 0;
            foreach (DatatypeCtor dc in datatype.Ctors)
            {
                MatchCaseStmt mcs;
                err = GenerateMatchCaseStmt(line, dc, body[i], out  mcs, flags[i]);
                if (err != null)
                    return err;
                cases.Add(mcs);
                line += mcs.Body.Count + 1;
                i++;
            }

            result = new MatchStmt(CreateToken("match", index, 0), CreateToken("=>", index, 0), ns, cases, false);

            return null;
        }

        private string GenerateMatchCaseStmt(int line, DatatypeCtor dtc, Solution solution, out MatchCaseStmt mcs, bool genBody)
        {
            Contract.Requires(dtc != null);
            List<CasePattern> casePatterns = new List<CasePattern>();
            mcs = null;
            dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);
            foreach (Dafny.Formal formal in dtc.Formals)
            {
                CasePattern cp;
                GenerateCasePattern(line, formal, out cp);
                casePatterns.Add(cp);
            }

            if (genBody)
            {
                List<Statement> body = new List<Statement>();
                if (solution != null)
                {
                    Atomic ac = solution.state.Copy();
                    body = ac.GetAllUpdated();
                }
                mcs = new MatchCaseStmt(CreateToken("cases", line, 0), dtc.CompileName, casePatterns, body);
            }
            else
                mcs = new MatchCaseStmt(CreateToken("cases", line, 0), dtc.CompileName, casePatterns, new List<Statement>());

            return null;
        }

        private void GenerateCasePattern(int line, Dafny.Formal formal, out CasePattern cp)
        {
            Contract.Requires(formal != null);
            formal = new Dafny.Formal(formal.tok, formal.Name, formal.Type, formal.InParam, formal.IsGhost);

            cp = new CasePattern(CreateToken(formal.Name, line, 0),
                                    new BoundVar(CreateToken(formal.Name, line, 0), formal.Name, new InferredTypeProxy()));
        }

        private static void InitCtorFlags(DatatypeDecl datatype, out bool[] flags)
        {
            flags = new bool[datatype.Ctors.Count];
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = false;
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

        private void RegisterLocals(List<Dafny.Formal> formals)
        {
            foreach (var formal in formals)
                globalContext.RegisterTempVariable(formal);
        }

        private void RegisterLocals(DatatypeDecl datatype, int index)
        {
            int i = 0;
            foreach (var ctor in datatype.Ctors)
            {
                if (i == index)
                {

                    foreach (var formal in ctor.Formals)
                    {
                        // register globals as name segments
                        globalContext.RegisterTempVariable(new Dafny.LocalVariable(formal.tok, formal.tok, formal.Name, new InferredTypeProxy(), formal.IsGhost));
                    }
                }
                i++;
            }
        }

        private void RemoveLocals(DatatypeDecl datatype, int index)
        {
            int i = 0;
            foreach (var ctor in datatype.Ctors)
            {
                if (i == index)
                {
                    foreach (var formal in ctor.Formals)
                    {
                        globalContext.RemoveTempVariable(formal);
                    }
                }
                i++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool CheckError(MatchStmt ms, ref bool[] ctorFlags, int ctor)
        {
            // if the error token has not changed since last iteration
            if (!ErrorChanged(program.GetErrorToken(), ms, ctor))
                return false;

            this.oldToken = program.GetErrorToken();
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