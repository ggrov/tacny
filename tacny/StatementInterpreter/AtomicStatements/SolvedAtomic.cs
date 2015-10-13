using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class SolvedAtomic : Atomic, IAtomicStmt
    {
        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Solved(st, ref solution_list);
        }

        public SolvedAtomic(Atomic atomic) : base(atomic) { }

        private string Solved(Statement st, ref List<Solution> solution_list)
        {
            TacnySolvedBlockStmt stmt = st as TacnySolvedBlockStmt;
            string err = null;
            List<Solution> result = null;
            err = ResolveBody(stmt.Body, out result);
            if (err != null)
                return err;
            for (int i = 0; i < result.Count; i++)
            {
                Solution sol = result[i];
                Atomic ac = this.Copy();
                Dafny.Program dprog = program.ParseProgram();
                sol.GenerateProgram(ref dprog);
                program.ClearBody(localContext.md);
                err = program.ResolveProgram();
                //program.MaybePrintProgram(dprog, null);
                // skip the solution if resolution failed    
                if (err != null)
                {
                    IncBadBranchCount();
                    continue;
                }
                //program.MaybePrintProgram(dprog, null);
                program.VerifyProgram();
                if (!program.HasError())
                {
                    // change back the context of the state
                    sol.state.localContext.tac_body = sol.state.localContext.tac.Body.Body;
                    sol.state.localContext.SetCounter(localContext.GetCounter());
                    solution_list.Add(sol);
                    return null;
                }
                IncBadBranchCount();
            }

            return null;
        }
    }
}
