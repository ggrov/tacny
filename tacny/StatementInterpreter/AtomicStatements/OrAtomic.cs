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
    class OrAtomic : Atomic, IAtomicStmt
    {
        public OrAtomic(Atomic atomic) : base(atomic)
        {

        }
        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Branch(st, ref solution_list);
        }


        private string Branch(Statement st, ref List<Solution> solution_list)
        {
            OrStmt os;
            string err;
            os = st as OrStmt;
            Tactic tac = localContext.tac;
            int index = localContext.GetCounter();
            List<Statement> body_list;

            body_list = localContext.GetFreshTacticBody();
            if (os.Blhs != null)
            {
                body_list.RemoveAt(index);
                body_list.InsertRange(index, os.Blhs);
            }
            else
            {
                UpdateStmt lhs = GenUpdateStmt(os.Lhss as ApplySuffix);
                Contract.Assert(lhs != null);
                body_list[index] = lhs;
            }

            solution_list.Add(CreateSolution(tac, body_list));

            body_list = localContext.GetFreshTacticBody();
            if (os.Brhs != null)
            {
                body_list.RemoveAt(index);
                body_list.InsertRange(index, os.Brhs);
            }
            else
            {
                UpdateStmt rhs = GenUpdateStmt(os.Rhs as ApplySuffix);
                Contract.Assert(rhs != null);
                body_list[index] = rhs;
            }

            solution_list.Add(CreateSolution(tac, body_list));

            return null;
        }



        private UpdateStmt GenUpdateStmt(ApplySuffix aps)
        {
            Contract.Requires(aps != null);
            return new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }

        private Solution CreateSolution(Tactic tac, List<Statement> newBody)
        {
            Tactic newTac = newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword,
                                        tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                                        tac.Decreases, new BlockStmt(tac.Body.Tok, tac.Body.EndTok, newBody),
                                        tac.Attributes, tac.SignatureEllipsis);
            Atomic newAtomic = this.Copy();
            newAtomic.localContext.tac = newTac;
            /* HACK */
            // decrase the tactic body counter
            // so the interpreter would execute newly inserted atomic
            newAtomic.localContext.DecCounter();

            return new Solution(newAtomic);
        }
    }
}
