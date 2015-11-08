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
        public override string FormatError(string error)
        {
            return "ERROR addif: " + error;
        }

        public IfAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Requires(st != null);
            Expression guard = ExtractGuard(st);
            if (guard == null)
                return "Unable to extract the while statement guard";
            /**
             * 
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable(guard))
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
            string err;
            List<Solution> result = null;
            Expression guard = ExtractGuard(loop);
            bool guard_res = false;

            // If literal guard
            if (guard is Dafny.LiteralExpr)
            {
                // we can only expect TRUE/FALSE literals
                Dafny.LiteralExpr litGuard = guard as Dafny.LiteralExpr;

                if (litGuard.Value is bool)
                {
                    guard_res = (bool)litGuard.Value;
                }

            }
            else
            {
                //resolve binary exp
            }
            // if the guard has been resolved to true resolve then body
            if (guard_res == true)
            {
                err = ResolveBody(loop.Thn, out result);
                // @HACK update the context of each result
                foreach (var item in result)
                {
                    item.state.localContext.tac_body = localContext.tac_body; // set the body 
                    item.state.localContext.SetCounter(localContext.GetCounter()); // set the counter
                }
                solution_list.InsertRange(0, result);
            }
            else if (guard_res == false && loop.Els != null)
            {
                // if else is a blockStmt
                if (loop.Els is BlockStmt)
                    err = ResolveBody(loop.Els as BlockStmt, out result);
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
            return null;
        }

        private string InsertIf(IfStmt ifStmt, ref List<Solution> solution_list)
        {
            IncTotalBranchCount();
            AddUpdated(ifStmt, Util.Copy.CopyIfStmt(ifStmt));

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
