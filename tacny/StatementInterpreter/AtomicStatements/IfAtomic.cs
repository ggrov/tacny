using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Util;

namespace Tacny
{
    class IfAtomic : BlockAtomic, IAtomicStmt
    {


        public IfAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Assert(ExtractGuard(st) != null, Util.Error.MkErr(st, 2));
            /**
             * 
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable())
            {
                return ExecuteIf(st as IfStmt, ref solution_list);
            }
            else
            {
                return InsertIf(st as IfStmt, ref solution_list);
            }
        }

        private string ExecuteIf(IfStmt loop, ref List<Solution> solution_list)
        {
            List<Solution> result = null;
            bool guard_res = false;
            guard_res = EvaluateGuard();
            // if the guard has been resolved to true resolve then body
            if (guard_res)
                ResolveBody(loop.Thn, out result);
            else if (!guard_res && loop.Els != null)
            {
                // if else is a blockStmt
                if (loop.Els is BlockStmt)
                    ResolveBody(loop.Els as BlockStmt, out result);
                else
                /**
                 * the if statement is of the following form: if(){ .. } else if(){ .. }
                 * replace the top_level if with the bottom if
                 * */
                {
                    List<Statement> new_body = ReplaceCurrentAtomic(loop.Els);
                    Solution sol = CreateSolution(new_body);
                    solution_list.Add(sol);
                }

            }

            // @HACK update the context of each result
            foreach (var item in result)
            {
                item.state.localContext.tac_body = localContext.tac_body; // set the body 
                item.state.localContext.SetCounter(localContext.GetCounter()); // set the counter
            }
            solution_list.InsertRange(0, result);
            return null;
        }

        /// <summary>
        /// Insert the if statement into dafny code
        /// </summary>
        /// <param name="ifStmt"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private string InsertIf(IfStmt ifStmt, ref List<Solution> solution_list)
        {
            Contract.Requires(ifStmt != null);
            ResolveExpression(this.guard);
            Expression guard = this.guard.TreeToExpression();

            AddUpdated(ifStmt, Util.Copy.CopyIfStmt(ReplaceGuard(ifStmt, guard)));
            IncTotalBranchCount();
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }

        private static IfStmt ReplaceGuard(IfStmt stmt, Expression new_guard)
        {
            return new IfStmt(stmt.Tok, stmt.EndTok, new_guard, stmt.Thn, stmt.Els);
        }
    }
}
