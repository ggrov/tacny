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
            foreach (var sol in result)
            {
                //Atomic ac = this.Copy();
                if (!GenerateAndVerify(sol))
                    continue;
                if (!globalContext.program.HasError())
                {
                    // change back the context of the state
                    sol.state.localContext.tac_body = localContext.tac.Body.Body;// sol.state.localContext.tac.Body.Body;
                    sol.state.localContext.tac = localContext.tac;
                    sol.state.localContext.SetCounter(localContext.GetCounter());
                    solution_list.Add(sol);
                    return;
                }
            }

        }
    }
}
