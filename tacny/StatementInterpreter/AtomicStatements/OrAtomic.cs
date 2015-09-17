using System;
using System.Collections.Generic;
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
            BlockStmt body = tac.Body;
            int index = localContext.GetCounter();
            List<Statement> body_l = new List<Statement>(body.Body.ToArray());
            Tactic newTac;
            Atomic newAtomic;
            UpdateStmt rhs = UpdateStmt(os.Rhs as ApplySuffix);

            // check if lhs is a single expression or a block stmt
            if (os.Body != null)
            {
                body_l.InsertRange(index, os.Body);
                newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword, tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                    tac.Decreases, new BlockStmt(body.Tok, body.EndTok, body_l), tac.Attributes, tac.SignatureEllipsis);

                newAtomic = this.Copy();
                newAtomic.localContext.tac = newTac;
                /* HACK */
                // decrase the tactic body counter
                // so the interpreter would execute newly inserted atomic
                newAtomic.localContext.DecCounter();
                /* END HACK */
                solution_list.Add(new Solution(newAtomic));
            }
            else
            {
                UpdateStmt lhs = UpdateStmt(os.Lhss as ApplySuffix);

                // replace the OrStmt with lhs
                body_l[index] = lhs;

                newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword, tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                    tac.Decreases, new BlockStmt(body.Tok, body.EndTok, body_l), tac.Attributes, tac.SignatureEllipsis);

                 newAtomic = this.Copy();
                 newAtomic.localContext.tac = newTac;
                /* HACK */
                // decrase the tactic body counter
                // so the interpreter would execute newly inserted atomic
                newAtomic.localContext.DecCounter();
                /* END HACK */
                solution_list.Add(new Solution(newAtomic));
            }

            body_l = new List<Statement>(body.Body.ToArray());
            body_l[index] = rhs;
            newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword, tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
                tac.Decreases, new BlockStmt(body.Tok, body.EndTok, body_l), tac.Attributes, tac.SignatureEllipsis);

            newAtomic = this.Copy();
            newAtomic.localContext.tac = newTac;
            /* HACK */
            // decrase the tactic body counter
            // so the interpreter would execute newly inserted atomic
            newAtomic.localContext.DecCounter();
            /* END HACK */

            solution_list.Add(new Solution(newAtomic));
            return null;
        }



        private UpdateStmt UpdateStmt(ApplySuffix aps)
        {
            return new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }


        
    }
}
