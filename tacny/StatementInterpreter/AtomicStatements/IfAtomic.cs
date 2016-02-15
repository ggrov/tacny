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

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Contract.Assert(ExtractGuard(st) != null, Util.Error.MkErr(st, 2));
            /**
             * 
             * Check if the loop guard can be resolved localy
             */
            if (IsResolvable())
                ExecuteIf(st as IfStmt, ref solution_list);
            else
                InsertIf(st as IfStmt, ref solution_list);
        }

        private void ExecuteIf(IfStmt loop, ref List<Solution> solution_list)
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
                    Solution sol = CreateTactic(new_body);
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
        }

        /// <summary>
        /// Insert the if statement into dafny code
        /// </summary>
        /// <param name="ifStmt"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private void InsertIf(IfStmt ifStmt, ref List<Solution> solution_list)
        {
            Contract.Requires(ifStmt != null);
            ResolveExpression(this.guard);
            Expression guard = this.guard.TreeToExpression();
            // resolve the if statement body
            List<Solution> resultThn = null;
            ResolveBody(ifStmt.Thn, out resultThn);
            List<Solution> resultEls = new List<Solution>();
            if (ifStmt.Els != null)
            {
                if (ifStmt.Els is BlockStmt)
                    ResolveBody(ifStmt.Els as BlockStmt, out resultEls);
                else
                    CallAction(ifStmt.Els, ref resultEls);
            }
            List<IfStmt> result = new List<IfStmt>();
            GenerateIfStmt(ifStmt, guard, resultThn, resultEls, ref result);

            foreach (var item in result)
            {
                Atomic ac = this.Copy();
                ac.AddUpdated(item, item);
                solution_list.Add(new Solution(ac));
            }

        }

        private static void GenerateIfStmt(IfStmt original, Expression guard, List<Solution> thn, List<Solution> els, ref List<IfStmt> result)
        {
            for (int i = 0; i < thn.Count; i++)
            {
                List<Statement> bodyList = thn[i].state.GetAllUpdated();
                BlockStmt thenBody = new BlockStmt(original.Thn.Tok, original.Thn.EndTok, bodyList);
                if (els != null)
                {
                    for (int j = 0; j < els.Count; j++)
                    {
                        List<Statement> elseList = els[i].state.GetAllUpdated();
                        Statement elseBody = null;
                        if (original.Els is BlockStmt)
                            elseBody = new BlockStmt(original.Els.Tok, original.Thn.EndTok, elseList);
                        else
                            elseBody = elseList[0];
                        result.Add(new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(thenBody), elseBody));
                    }
                }
                else
                {
                    result.Add(new IfStmt(original.Tok, original.EndTok, Util.Copy.CopyExpression(guard), Util.Copy.CopyBlockStmt(thenBody), null));
                }
            }
        }
    }
}
