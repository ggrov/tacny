using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
namespace Tacny
{
    class CompositionAction : Action
    {
        public CompositionAction(Action action) : base(action) { }

        
        
        public string Composition(Statement st, ref SolutionTree solution_tree)
        {
            IfStmt if_stmt = st as IfStmt;
            WhileStmt while_stmt = st as WhileStmt;
            ConditionalAction.ConditionResult res;
            string err;

            if (if_stmt != null)
            {
                Expression guard = if_stmt.Guard;
                Atomic guard_type;
                // get guard type
                err = AnalyseGuard(guard, out guard_type);
                if (err != null)
                    return "composition: " + err;
                err = CallGuard(guard_type, ref solution_tree, out res);
            }
            else if (while_stmt != null) { }
            else
                return "composition: Internal error unexpected Statement type: " + st.GetType();
            return null;
        }

        private string AnalyseGuard(Expression guard, out Atomic type)
        {
            Expression exp;
            ApplySuffix ass;
            type = Atomic.UNDEFINED;
            
            if (guard is ParensExpression)
                exp = ((ParensExpression)guard).E;
            else
                exp = guard;

            ass = exp as ApplySuffix;
            if (ass != null)
                type = GetStatementType(ass);
            else
                return "Invalid composition guard; Expected atomic statement; Received " + exp.GetType();

            return null;
        }

        private string CallGuard(Atomic type, ref SolutionTree solution_tree, out ConditionalAction.ConditionResult result)
        {
            string err;
            switch (type)
            {
                case Atomic.IS_VALID:
                    ConditionalAction ca = new ConditionalAction(this);
                    err = ca.IsValid(ref solution_tree, out result);
                    break;
                default:
                    throw new cce.UnreachableException();
            }
            return err;
        }
    }
}
