using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class WhileAtomic : BlockAtomic, IAtomicStmt
    {
        public WhileAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Assert(ExtractGuard(st) != null, Error.MkErr(st, 2));
            /**
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable())
                ExecuteLoop(st as WhileStmt, ref solution_list);
            else
                InsertLoop(st as WhileStmt, ref solution_list);
        }


        private void ExecuteLoop(WhileStmt whileStmt, ref List<Solution> solution_list)
        {
            List<Solution> result = null;
            bool guard_res = false;
            guard_res = EvaluateGuard();
            // if the guard has been resolved to true resolve then body
            if (guard_res)
            {
                ResolveBody(whileStmt.Body, out result);

                // @HACK update the context of each result
                foreach (var item in result)
                {
                    item.state.localContext.tacticBody = localContext.tacticBody; // set the body 
                    item.state.localContext.tac_call = localContext.tac_call;
                    item.state.localContext.SetCounter(localContext.GetCounter());
                }

                solution_list.AddRange(result);
            }
        }

        private void InsertLoop(WhileStmt whileStmt, ref List<Solution> solution_list)
        {
            Contract.Requires(whileStmt != null);
            ResolveExpression(this.guard);
            Expression guard = this.guard.TreeToExpression();
            List<Solution> solList;
            ResolveBody(whileStmt.Body, out solList);
            List<WhileStmt> result = new List<WhileStmt>();
            GenerateWhileStmt(whileStmt, guard, solList, ref result);

            foreach (var item in result)
            {
                Atomic ac = Copy();
                ac.AddUpdated(item, item);
                solution_list.Add(new Solution(ac));
            }
        }

        private static WhileStmt ReplaceGuard(WhileStmt stmt, Expression new_guard)
        {
            return new WhileStmt(stmt.Tok, stmt.EndTok, new_guard, stmt.Invariants, stmt.Decreases, stmt.Mod, stmt.Body);
        }

        private static void GenerateWhileStmt(WhileStmt original, Expression guard, List<Solution> body, ref List<WhileStmt> result)
        {
            for (int i = 0; i < body.Count; i++)
            {
                List<Statement> bodyList = body[i].state.GetAllUpdated();
                BlockStmt thenBody = new BlockStmt(original.Body.Tok, original.Body.EndTok, bodyList);
                result.Add(new WhileStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), original.Invariants, original.Decreases, original.Mod, Util.Copy.CopyBlockStmt(thenBody)));
            }
        }
    }
}
