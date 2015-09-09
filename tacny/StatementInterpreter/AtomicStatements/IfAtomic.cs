using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class IfAtomic : Atomic
    {
        public override string FormatError(string error)
        {
            return "ERROR addif: " + error;
        }

        public IfAtomic(Atomic atomic) : base(atomic) { }

        public string AddIf(TacnyIfBlockStmt st, ref List<Solution> solution_list)
        {
            IfStmt iss;
            BlockStmt thn = null;
            BlockStmt els = null;

            if (st.Body.Body.Count < 1)
                return FormatError("addif body can not be empty");

            thn = new BlockStmt(st.Tok, st.EndTok, new List<Statement>() { st.Body.Body[0] });

            if (st.Body.Body.Count > 1)
                els = new BlockStmt(st.Tok, st.EndTok, new List<Statement>() { st.Body.Body[1] });

            

            GenerateIfStmt(st.Tok.line, st.Guard, thn, els, out iss);
            AddUpdated(iss, iss);
            solution_list.Add(new Solution(this.Copy()));

            return null;
        }

        private void GenerateIfStmt(int line, Expression guard, BlockStmt thn, BlockStmt els, out IfStmt ifStmt)
        {
            Contract.Requires(guard != null && thn != null);
            ifStmt = new IfStmt(CreateToken("if", line, 0), CreateToken("}", line, 0), guard, thn, els);
        }
    }
}
