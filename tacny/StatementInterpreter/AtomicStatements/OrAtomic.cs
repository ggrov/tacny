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
        { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Branch(st, ref solution_list);
        }

        private void Branch(Statement st, ref List<Solution> solution_list)
        {
            OrStmt os;
            os = st as OrStmt;
            List<Statement> body_list;

            // if left hand side is a block insert all the statements to the new body list
            if (os.Blhs != null)
            {
                body_list = ReplaceCurrentAtomic(os.Blhs);
                solution_list.Add(CreateSolution(body_list));
            }
            else // generate new updatestmt and insert it to body list
            {
                UpdateStmt lhs = GenUpdateStmt(os.Lhss as ApplySuffix);
                Contract.Assert(lhs != null);
                body_list = ReplaceCurrentAtomic(lhs);
                StatementRegister.Atomic type = StatementRegister.GetAtomicType(lhs);
                switch(type)
                {
                    case StatementRegister.Atomic.ID:
                        solution_list.Add(CreateSolution(body_list, false));
                        return;
                    case StatementRegister.Atomic.FAIL:
                         break;
                    case StatementRegister.Atomic.UNDEFINED:
                         //return "OR: undefined lhs statement";
                    default:
                        solution_list.Add(CreateSolution(body_list));
                        break;
                }   
                
            }

            if (os.Brhs != null)
            {
                body_list = ReplaceCurrentAtomic(os.Brhs);
                solution_list.Add(CreateSolution(body_list));
            }
            else
            {
                UpdateStmt rhs = GenUpdateStmt(os.Rhs as ApplySuffix);
                Contract.Assert(rhs != null);
                body_list = ReplaceCurrentAtomic(rhs);
                StatementRegister.Atomic type = StatementRegister.GetAtomicType(rhs);
                switch (type)
                {
                    case StatementRegister.Atomic.ID:
                        break;
                    case StatementRegister.Atomic.FAIL:
                        break;
                    case StatementRegister.Atomic.UNDEFINED:
                        //return "OR: undefined rhs statement";
                    default:
                        solution_list.Add(CreateSolution(body_list));
                        break;
                }
            }
        }

        private UpdateStmt GenUpdateStmt(ApplySuffix aps)
        {
            Contract.Requires(aps != null);
            return new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }
    }
}
