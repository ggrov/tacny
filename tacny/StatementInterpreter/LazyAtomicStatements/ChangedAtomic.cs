using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Dafny = Microsoft.Dafny;
namespace LazyTacny
{
    class ChangedAtomic : Atomic, IAtomicLazyStmt
    {
        public ChangedAtomic(Atomic atomic) : base(atomic) { }


        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            throw new NotImplementedException();
        }

        //private IEnumerable<Solution> Changed(Statement st, Solution solution)
        //{
        //    TacnyChangedBlockStmt stmt = st as TacnyChangedBlockStmt;
        //    List<Solution> result = null;
        //    ResolveBody(stmt.Body, out result);
        //    foreach (var sol in result)
        //    {
        //        if(sol.state.localContext.updated_statements.Count > 0 || sol.state.localContext.new_target != null)
        //        {
        //            // change back the context of the state
        //            sol.state.localContext.tac_body = localContext.tac.Body.Body;// sol.state.localContext.tac.Body.Body;
        //            sol.state.localContext.tac = localContext.tac;
        //            sol.state.localContext.SetCounter(localContext.GetCounter());
        //            yield return sol;
        //            //return;
        //        }
        //    }
        //}
    }
}
