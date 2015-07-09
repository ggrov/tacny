using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class IfAction : Action
    {
        public IfAction(Action action) : base(action) { }

        public string AddIf(TacnyIfBlockStmt st, ref SolutionTree solution_tree)
        {
            Contract.Requires(st.Body.Body.Count >= 1);
            IfStmt iss;
            string err;
            BlockStmt thn;
            BlockStmt els;
            List<Statement> thn_body = new List<Statement>();
            List<Statement> els_body = new List<Statement>();
            if (st.Body.Body.Count >= 1)
                thn_body.Add(st.Body.Body[0]);
            thn = new BlockStmt(st.Tok, st.EndTok, thn_body);

            if (st.Body.Body.Count > 1)
                els_body.Add(st.Body.Body[1]);

            els = new BlockStmt(st.Tok, st.EndTok, els_body);

            GenerateIfStmt(st.Tok.line, st.Guard, thn, els, out iss);
            updated_statements.Add(iss, iss);

            solution_tree.AddChild(new SolutionTree(this, solution_tree, st));

            return null;
        }

        private void GenerateIfStmt(int line, Expression guard, BlockStmt thn, BlockStmt els, out IfStmt ifStmt)
        {
            Contract.Requires(guard != null);
            ifStmt = null;


            Token tok = new Token();
            tok.val = "if";
            tok.line = line;
            Token  end_tok = new Token();
            end_tok.val = "}";
            end_tok.line = line;

            ifStmt = new IfStmt(tok, end_tok, guard, thn, els);
        }
    }
}
