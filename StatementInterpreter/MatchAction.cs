using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class MatchAction : Action
    {
        public MatchAction(Action action) : base(action) { }

        public string GenerateMatch(Statement st, ref SolutionTree solution_tree)
        {
            IVariable lv = null;
            List<Expression> call_arguments;
            DatatypeDecl datatype = null;
            MatchStmt ms;
            string datatype_name;
            string err;

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "generate_match: " + err;
            
            if (call_arguments.Count != 1)
                return "generate_match: Wrong number of method arguments; Expected 1 got " + call_arguments.Count;

            NameSegment ns = call_arguments[0] as NameSegment;
            if (ns == null)
                return "generate_match: unexpected argument";

            // messy way to do this. but works for now
            Dafny.Formal formal = (Dafny.Formal)GetLocalKeyByName(ns);
            if (formal.Type.Equals(Dafny.Type.Bool))
            {

            }
            else
            {
                datatype_name = formal.Type.ToString();

                if (!global_variables.ContainsKey(datatype_name))
                    return "generate_match: global datatype " + ns.Name + " is not defined";

                datatype = global_variables[datatype_name];
                ns = (NameSegment)local_variables[formal];
                GenerateMatchStmt(ns, datatype, out ms);

                updated_statements.Add(ms, ms);
            }
            

            solution_tree.AddChild(new SolutionTree(this, solution_tree, st));

            return null;
        }

        private void GenerateMatchStmt(NameSegment ns, DatatypeDecl datatype, out MatchStmt result)
        {
            Contract.Requires(ns != null);
            Contract.Requires(datatype != null);

            List<MatchCaseStmt> cases = new List<MatchCaseStmt>();
            result = null;
            Token tok = new Token();
            Token end_tok = new Token();
            tok.val = "match";
            tok.line = tac_call.Tok.line;
            tok.col = this.tac_call.Tok.col;
            end_tok.val = "=>";
            int line = tac_call.Tok.line + 1;
            foreach (DatatypeCtor dc in datatype.Ctors)
            {
                MatchCaseStmt mcs;
                GenerateMatchCaseStmt(line, dc, out mcs);
                cases.Add(mcs);
            }

            result = new MatchStmt(tok, end_tok, ns, cases, false);
        }

        private static void GenerateMatchCaseStmt(int line, DatatypeCtor dtc, out MatchCaseStmt mcs)
        {
            Contract.Requires(dtc != null);
            List<CasePattern> casePatterns = new List<CasePattern>();
            mcs = null;
            Token tok = new Token();
            tok.val = "case";
            tok.line = line;
            foreach (Dafny.Formal formal in dtc.Formals)
            {
                CasePattern cp;
                GenerateCasePattern(line++, formal, out cp);
                casePatterns.Add(cp);

            }

            mcs = new MatchCaseStmt(tok, dtc.CompileName, casePatterns, new List<Statement>());
        }

        private static void GenerateCasePattern(int line, Dafny.Formal formal, out CasePattern cp)
        {
            Contract.Requires(formal != null);
            cp = null;
            Token tok = new Token();
            tok.val = formal.Name;
            tok.line = line;
            BoundVar bv = new BoundVar(tok, formal.Name, new InferredTypeProxy());
            cp = new CasePattern(tok, bv);
        }
    }
}
