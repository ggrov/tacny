using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Util;

namespace Tacny
{
    class WhileAtomic : BlockAtomic, IAtomicStmt
    {
        public WhileAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            if (ExtractGuard(st) == null)
            {
                Util.Printer.Error(st, "Unable to extract the statement guard");
                return "Unable to extract the while statement guard";
            }


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
            string err;
            List<Solution> result = null;
            bool guard_res = false;
            guard_res = EvaluateGuard();
            // if the guard has been resolved to true resolve then body
            if (guard_res)
            {
                err = ResolveBody(whileStmt.Body, out result);
                if (err != null)
                {
                    Util.Printer.Error(whileStmt, err);
                    return err; //temp
                }
                // @HACK update the context of each result
                foreach (var item in result)
                {
                    item.state.IncTotalBranchCount();
                    item.state.localContext.tac_body = localContext.tac_body; // set the body 
                    // add a copy of a solution after each iteration
                    solution_list.Add(new Solution(item.state.Copy()));
                    item.state.IncTotalBranchCount();
                    //item.state.localContext.tac_call = localContext.tac_call;
                    item.state.localContext.SetCounter(localContext.GetCounter() - 1); // roll back the counter
                }

                solution_list.AddRange(result);
            }
            else
            {
                // do nothing
            }
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
