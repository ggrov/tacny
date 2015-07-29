using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class MatchAction : Action
    {
        public override string FormatError(string error)
        {
            return "ERROR cases: " + error;
        }

        public MatchAction(Action action) : base(action) { }

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
            MatchStmt ms;
            ParensExpression guard;
            NameSegment ns;
            string datatype_name;

            Program prog = program.newProgram();
            bool[] ctorFlags;  // used to keep track of which cases statements require a body
            guard = st.Guard as ParensExpression;

            if (guard == null)
                ns = st.Guard as NameSegment;
            else
                ns = guard.E as NameSegment;

            if (ns == null)
                return FormatError("unexpected argument");

            Dafny.Formal formal = (Dafny.Formal)GetLocalKeyByName(ns);

            datatype_name = formal.Type.ToString();

            if (!global_variables.ContainsKey(datatype_name))
                return FormatError("global datatype " + ns.Name + " is not defined");

            datatype = global_variables[datatype_name];
            initFlags(datatype, out ctorFlags);
            
            ns = (NameSegment)local_variables[formal];
            // TODO: check if NS if of correct type
            GenerateMatchStmt(new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype,
                                        st.Body.Body, out ms, ctorFlags);

            updated_statements.Add(ms, ms);

            Solution solution = new Solution(this.Copy(), true, null);
            
            Dafny.Program dprog = prog.program;
            solution.GenerateProgram(ref dprog);
            prog.ClearBody();
            prog.VerifyProgram();
            //prog.MaybePrintProgram(dprog, null);
            if (prog.HasError() && st.Body.Body.Count > 0)
            {
                while (prog.HasError())
                {
                    // if the error token has not changed since last iteration
                    // break
                    if (AnalyseError(prog.GetErrorToken()))
                        break;
                    this.oldToken = prog.GetErrorToken();
                    int index = GetErrorIndex(oldToken, ms);
                    // the verification error is not caused by the match stmt
                    if (index == -1)
                        break;
                    ctorFlags[index] = true;
                    updated_statements.Remove(ms);
                    GenerateMatchStmt(new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, st.Body.Body, out ms, ctorFlags);
                    updated_statements.Add(ms, ms);
                    solution = new Solution(this.Copy(), true, null);
                    prog.program = prog.parseProgram();
                    dprog = prog.program;
                    solution.GenerateProgram(ref dprog);
                    prog.VerifyProgram();
                    prog.MaybePrintProgram(dprog, null);
                }
            }
            /*
             * HACK Recreate the match block as the old one was modified by the resolver
             * */
            updated_statements.Remove(ms);
            GenerateMatchStmt(new NameSegment(ns.tok, ns.Name, ns.OptTypeArguments), datatype, st.Body.Body, out ms, ctorFlags);
            updated_statements.Add(ms, ms);
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
            int line = tac_call.Tok.line + 1;
            int i = 0;
            foreach (DatatypeCtor dc in datatype.Ctors)
            {
                MatchCaseStmt mcs;
                if(flags[i])
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


        private void initFlags(DatatypeDecl datatype, out bool [] flags)
        {
            flags = new bool[datatype.Ctors.Count];
            for (int i = 0; i < flags.Length; i++)
            {
                flags[i] = false;
            }
        }
    }
}
