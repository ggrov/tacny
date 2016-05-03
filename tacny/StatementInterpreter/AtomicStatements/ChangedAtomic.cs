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
                if(sol.state.localContext.generatedStatements.Count > 0 || sol.state.localContext.newTarget != null)
                {
                    // change back the context of the state
                    if (localContext.tactic is Tactic)
                    {
                        sol.state.localContext.tacticBody = ((Tactic)localContext.tactic).Body.Body;// sol.state.localContext.tac.Body.Body;
                    }
                    sol.state.localContext.tactic = localContext.tactic;
                    sol.state.localContext.SetCounter(localContext.GetCounter());
                    solution_list.Add(sol);
                    //return;
                }
            }
        }
    }
}
