using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Util;
using System.Diagnostics;
using Tacny;

namespace LazyTacny
{
    class WhileAtomic : BlockAtomic, IAtomicLazyStmt
    {
        public WhileAtomic(Atomic atomic) : base(atomic) { }

        

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            Contract.Assert(ExtractGuard(st) != null, Util.Error.MkErr(st, 2));
            
            /**
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable())
                foreach (var item in ExecuteLoop(st as WhileStmt, solution))
                    yield return item;
            else
                foreach (var item in InsertLoop(st as WhileStmt, solution))
                    yield return item;
            
            yield break;
        }

        private IEnumerable<Solution> ExecuteLoop(WhileStmt whileStmt, Solution solution)
        {
            bool guard_res = false;
            guard_res = EvaluateGuard();
            // if the guard has been resolved to true resolve then body
            if (guard_res)
            {
                
                foreach (var item in ResolveBody(whileStmt.Body))
                {
                    item.state.dynamicContext.isPartialyResolved = true;
                    yield return item;                  
                }
            }

            yield break;
        }

        private IEnumerable<Solution> InsertLoop(WhileStmt whileStmt, Solution solution)
        {
            Contract.Requires(whileStmt != null);
            ResolveExpression(this.guard);
            Expression guard = this.guard.TreeToExpression();

            foreach (var item in ResolveBody(whileStmt.Body))
            {
                var result = GenerateWhileStmt(whileStmt, guard, item);
                Atomic ac = this.Copy();
                ac.AddUpdated(result, result);
                yield return new Solution(ac);
            }
            yield break;
        }

        private static WhileStmt ReplaceGuard(WhileStmt stmt, Expression new_guard)
        {
            return new WhileStmt(stmt.Tok, stmt.EndTok, new_guard, stmt.Invariants, stmt.Decreases, stmt.Mod, stmt.Body);
        }

        private static WhileStmt GenerateWhileStmt(WhileStmt original, Expression guard, Solution body)
        {
            List<Statement> bodyList = body.state.GetAllUpdated();
            BlockStmt thenBody = new BlockStmt(original.Body.Tok, original.Body.EndTok, bodyList);
            return new WhileStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), original.Invariants, original.Decreases, original.Mod, Util.Copy.CopyBlockStmt(thenBody));
        }
    }
}
