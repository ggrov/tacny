using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class CompositionAtomic : Atomic, IAtomicStmt
    {

        public CompositionAtomic(Atomic atomic) : base(atomic) { }

        public override string FormatError(string error)
        {
            return "ERROR composition: " + error;
        }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Composition(st, ref solution_list);
        }

        public string Composition(Statement st, ref List<Solution> solution_list)
        {
            IfStmt if_stmt = st as IfStmt;
            WhileStmt while_stmt = st as WhileStmt;
            ConditionalAtomic.ConditionResult res;
            string err;

            if (if_stmt != null)
            {
                Expression guard = if_stmt.Guard;
                StatementRegister.Atomic guard_type;
                // get guard type
                err = AnalyseGuard(guard, out guard_type);
                if (err != null)
                    return FormatError(err);
                // if the guard is not tacny atomic add the if statement "as is"
                if (guard_type == StatementRegister.Atomic.UNDEFINED)
                {
                    AddUpdated(st, st);
                    solution_list.Add(new Solution(this.Copy(), true, null));
                    return null;
                }

                err = CallGuard(guard_type, out res);
                // todo analyse else
                // todo execute bodies
                
            }
            else if (while_stmt != null) { 
            // todo
            }
            else
                return FormatError("Internal error unexpected Statement type: " + st.GetType());


            
            return null;
        }

        private string AnalyseGuard(Expression guard, out StatementRegister.Atomic type)
        {
            Expression exp;
            ApplySuffix ass;
            type = StatementRegister.Atomic.UNDEFINED;
            
            if (guard is ParensExpression)
                exp = ((ParensExpression)guard).E;
            else
                exp = guard;

            ass = exp as ApplySuffix;
            if (ass != null)
                type = StatementRegister.GetAtomicType(ass.Lhs.tok.val);

            return null;
        }

        private string CallGuard(StatementRegister.Atomic type, out ConditionalAtomic.ConditionResult result)
        {
            string err;
            switch (type)
            {
                case StatementRegister.Atomic.IS_VALID:
                    ConditionalAtomic ca = new ConditionalAtomic(this);
                    err = ca.IsValid(out result);
                    break;
                default:
                    throw new cce.UnreachableException();
            }
            return err;
        }


    }
}
