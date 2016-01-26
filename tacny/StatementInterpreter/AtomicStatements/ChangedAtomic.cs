using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Dafny = Microsoft.Dafny;
namespace Tacny
{
    class ChangedAtomic : Atomic, IAtomicStmt
    {
        public ChangedAtomic(Atomic atomic) : base(atomic) { }


        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Changed(st, ref solution_list);
        }


        private void Changed(Statement st, ref List<Solution> solution_list)
        {
            TacnyChangedBlockStmt stmt = st as TacnyChangedBlockStmt;
            List<Solution> result = null;
            ResolveBody(stmt.Body, out result);
            foreach (var sol in result)
            {
                if(sol.state.localContext.updated_statements.Count > 0 || sol.state.localContext.new_target != null)
                {
                    // change back the context of the state
                    sol.state.localContext.tac_body = localContext.tac.Body.Body;// sol.state.localContext.tac.Body.Body;
                    sol.state.localContext.tac = localContext.tac;
                    sol.state.localContext.SetCounter(localContext.GetCounter());
                    solution_list.Add(sol);
                    //return;
                }
            }
        }
    }
}
