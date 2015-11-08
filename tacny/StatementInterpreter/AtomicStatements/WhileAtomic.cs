using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;


namespace Tacny
{
    class WhileAtomic : BlockAtomic, IAtomicStmt
    {
        public WhileAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            Expression guard = ExtractGuard(st);
            if(guard == null)
                return "Unable to extract the while statement guard";
            /**
             * 
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable(guard))
            {
                return ExecuteLoop(st as WhileStmt, guard, ref solution_list);
            }
            else
            {
                return InsertLoop(st as WhileStmt, ref solution_list);
            }
        }


        private string ExecuteLoop(WhileStmt loop, Expression guard, ref List<Solution> solution_list)
        {


            return null;
        }

        private string InsertLoop(WhileStmt loop, ref List<Solution> solution_list)
        {
            IncTotalBranchCount();
            AddUpdated(loop, loop);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
