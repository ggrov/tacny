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
        public SolvedAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Solved(st, ref solution_list);
        }

        private void Solved(Statement st, ref List<Solution> solution_list)
        {
            TacnySolvedBlockStmt stmt = st as TacnySolvedBlockStmt;
            List<Solution> result = null;
            ResolveBody(stmt.Body, out result);
          
            for (int i = 0; i < result.Count; i++)
            {
                Solution sol = result[i];
                Atomic ac = this.Copy();
                Dafny.Program dprog = program.ParseProgram();
                sol.GenerateProgram(ref dprog);
                program.ClearBody(localContext.md);
                //program.MaybePrintProgram(dprog, null);
                program.ResolveProgram();
                // skip the solution if resolution failed    
                if (!program.resolved)
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
                    return;
                }
                IncBadBranchCount();
            }

        }
    }
}
