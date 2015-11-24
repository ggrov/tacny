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
            if(ExtractGuard(st) == null)
                return "Unable to extract the while statement guard";

            
            /**
             * 
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable())
            {
                return ExecuteLoop(st as WhileStmt, ref solution_list);
            }
            else
            {
                return InsertLoop(st as WhileStmt, ref solution_list);
            }
        }


        private string ExecuteLoop(WhileStmt whileStmt, ref List<Solution> solution_list)
        {
            return null;
        }

        private string InsertLoop(WhileStmt whileStmt, ref List<Solution> solution_list)
        {
            Contract.Requires(whileStmt != null);
            ResolveExpression(this.guard);
            Expression guard = this.guard.TreeToExpression();

            AddUpdated(whileStmt, Util.Copy.CopyWhileStmt(ReplaceGuard(whileStmt, guard)));
            IncTotalBranchCount();
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }

        private static WhileStmt ReplaceGuard(WhileStmt stmt, Expression new_guard)
        {
            return new WhileStmt(stmt.Tok, stmt.EndTok, new_guard, stmt.Invariants, stmt.Decreases, stmt.Mod, stmt.Body);
        }
    }
}
