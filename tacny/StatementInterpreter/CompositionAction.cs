using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class CompositionAction : Action
    {
        public CompositionAction(Action action) : base(action) { }

        public override string FormatError(string error)
        {
            return "ERROR composition: " + error;
        }

        
        public string Composition(Statement st, ref List<Solution> solution_list)
        {
            IfStmt if_stmt = st as IfStmt;
            WhileStmt while_stmt = st as WhileStmt;
            ConditionalAction.ConditionResult res;
            string err;

            if (if_stmt != null)
            {
                Expression guard = if_stmt.Guard;
                StatementRegister.Atomic guard_type;
                // get guard type
                err = AnalyseGuard(guard, out guard_type);
                if (err != null)
                    return FormatError(err);
                if (guard_type == StatementRegister.Atomic.UNDEFINED)
                {
                    updated_statements.Add(st, st);
                    solution_list.Add(new Solution(this.Copy(), true, null));
                    return null;
                }

                err = CallGuard(guard_type, out res);
            }
            else if (while_stmt != null) { }
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

        private string CallGuard(StatementRegister.Atomic type, out ConditionalAction.ConditionResult result)
        {
            string err;
            switch (type)
            {
                case StatementRegister.Atomic.IS_VALID:
                    ConditionalAction ca = new ConditionalAction(this);
                    err = ca.IsValid(out result);
                    break;
                default:
                    throw new cce.UnreachableException();
            }
            return err;
        }
    }
}
