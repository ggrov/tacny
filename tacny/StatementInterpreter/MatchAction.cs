using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

// todo cases multiple tac calls, cases within cases, tac calls from cases etc.
// update 

namespace Tacny
{
    class MatchAction : Action, AtomicStmt
    {
        public override string FormatError(string error)
        {
            return "ERROR cases: " + error;
        }

        public MatchAction(Action action) : base(action) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            string err = GenerateMatch(st as TacnyCasesBlockStmt, ref solution_list);
            return err;
        }

        private Token oldToken = null;
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
        /**
         * Check whether the error token has been updated
         **/
        public bool AnalyseError(Token errorToken)
        {
            if (oldToken == null)
                return false;
            if (errorToken.val == oldToken.val && errorToken.line == oldToken.line)
                return true;
            return false;
        }

        public string GenerateMatch(TacnyCasesBlockStmt st, ref List<Solution> solution_list)
        {
            DatatypeDecl datatype = null;
            MatchStmt ms = null;
            ParensExpression guard;
            NameSegment ns;
            string datatype_name;
            string err;
            List<Statement> body;
            Solution solution;
            Dafny.Program dprog;

            bool[] ctorFlags; //localContext.ctorFlags; // used to keep track of which cases statements require a body
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

            if (!globalContext.ContainsGlobalKey(datatype_name))
                return FormatError("global datatype " + ns.Name + " is not defined");

            datatype = globalContext.GetGlobal(datatype_name);
            initFlags(datatype, out ctorFlags);

            ns = localContext.GetLocalValueByName(formal) as NameSegment;
            
            while (program.HasError())
            {

                // if the error token has not changed since last iteration
                // break
                if (AnalyseError(program.GetErrorToken()))
                    break;
                this.oldToken = program.GetErrorToken();
                if (oldToken != null)
                {
                    int index = GetErrorIndex(oldToken, ms);
                    // the verification error is not caused by the match stmt
                    if (index == -1)
                        break;
                    ctorFlags[index] = true;
                }
                
                err = ResolveBody(st.Body, out body);
                GenerateMatchStmt(new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, body, out ms, ctorFlags);
                AddUpdated(ms, ms);
                solution = new Solution(this.Copy(), true, null);
                dprog = program.ParseProgram();
                solution.GenerateProgram(ref dprog);
                program.ClearBody(localContext.md);
                program.VerifyProgram();
                program.MaybePrintProgram(dprog, null);
                RemoveUpdated(ms); 
            }
            
            /*
             * HACK Recreate the match block as the old one was modified by the resolver
             */
            RemoveUpdated(ms);
            err = ResolveBody(st.Body, out body);
            GenerateMatchStmt(new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, body, out ms, ctorFlags);
            AddUpdated(ms, ms);
            solution = new Solution(this.Copy(), true, null);

            solution_list.Add(solution);

            return null;
        }

        private void GenerateMatchStmt(NameSegment ns, DatatypeDecl datatype, List<Statement> body, out MatchStmt result, bool[] flags)
        {
            Contract.Requires(ns != null);
            Contract.Requires(datatype != null);

            List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
            result = null;
            UpdateStmt tac_call = GetTacticCall();
            int line = tac_call.Tok.line + 1;
            int i = 0;
            foreach (DatatypeCtor dc in datatype.Ctors)
            {
                MatchCaseStmt mcs;
                if (flags[i])
                    GenerateMatchCaseStmt(line, dc, body, out mcs);
                else
                    GenerateMatchCaseStmt(line, dc, new List<Statement>(), out mcs);
                cases.Add(mcs);
                i++;
            }

            result = new MatchStmt(CreateToken("match", tac_call.Tok.line, tac_call.Tok.col), CreateToken("=>", tac_call.Tok.line, 0), ns, cases, false);
        }

        private static void GenerateMatchCaseStmt(int line, DatatypeCtor dtc, List<Statement> body, out MatchCaseStmt mcs)
        {
            Contract.Requires(dtc != null);
            List<CasePattern> casePatterns = new List<CasePattern>();
            mcs = null;
            dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);
            foreach (Dafny.Formal formal in dtc.Formals)
            {
                CasePattern cp;
                GenerateCasePattern(line++, formal, out cp);
                casePatterns.Add(cp);

            }

            mcs = new MatchCaseStmt(CreateToken("cases", line, 0), dtc.CompileName, casePatterns, body);
        }

        private static void GenerateCasePattern(int line, Dafny.Formal formal, out CasePattern cp)
        {
            Contract.Requires(formal != null);
            formal = new Dafny.Formal(formal.tok, formal.Name, formal.Type, formal.InParam, formal.IsGhost);

            cp = new CasePattern(CreateToken(formal.Name, line, 0),
                                    new BoundVar(CreateToken(formal.Name, line, 0), formal.Name, new InferredTypeProxy()));
        }


        private void initFlags(DatatypeDecl datatype, out bool[] flags)
        {
            flags = new bool[datatype.Ctors.Count];
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = false;
            }
        }
    }
}
