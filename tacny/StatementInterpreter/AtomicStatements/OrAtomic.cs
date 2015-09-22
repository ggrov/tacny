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
            // if left hand side is a block insert all the statements to the new body list
            if (os.Blhs != null)
            {
                body_list.RemoveAt(index);
                body_list.InsertRange(index, os.Blhs);
            }
            else // generate new updatestmt and insert it to body list
            {
                UpdateStmt lhs = GenUpdateStmt(os.Lhss as ApplySuffix);
                Contract.Assert(lhs != null);
                body_list[index] = lhs;
                StatementRegister.Atomic type = StatementRegister.GetAtomicType(lhs);
                switch(type)
                {
                    case StatementRegister.Atomic.ID:
                        solution_list.Add(CreateSolution(tac, body_list, false));
                        return null;
                    case StatementRegister.Atomic.FAIL:
                         break;
                    case StatementRegister.Atomic.UNDEFINED:
                         //return "OR: undefined lhs statement";
                    default:
                        solution_list.Add(CreateSolution(tac, body_list));
                        break;
                }   
                
            }
                        
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
                StatementRegister.Atomic type = StatementRegister.GetAtomicType(rhs);
                switch (type)
                {
                    case StatementRegister.Atomic.ID:
                        return "OR: Id() can only be used on the lhs of the statement";
                    case StatementRegister.Atomic.FAIL:
                        break;
                    case StatementRegister.Atomic.UNDEFINED:
                        //return "OR: undefined rhs statement";
                    default:
                        solution_list.Add(CreateSolution(tac, body_list));
                        break;
                }
            }

            return null;
        }



        private UpdateStmt GenUpdateStmt(ApplySuffix aps)
        {
            Contract.Requires(aps != null);
            return new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }

        private Solution CreateSolution(Tactic tac, List<Statement> newBody, bool decCounter = true)
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
            if(decCounter)
                newAtomic.localContext.DecCounter();

            return new Solution(newAtomic);
        }
    }
}
