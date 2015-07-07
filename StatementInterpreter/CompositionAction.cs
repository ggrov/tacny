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
            IfStmt if_stmt = null;
            WhileStmt while_stmt = null;
            string err;
           
            if (st is IfStmt)
                if_stmt = (IfStmt)st;
            else if (st is WhileStmt)
                while_stmt = (WhileStmt)st;
            else
                return "composition: Internal error unexpected Statement type: " + st.GetType();

            if (if_stmt != null)
            {
                Expression guard = if_stmt.Guard;
                Atomic guard_type;
                // get guard type
                err = AnalyseGuard(guard, out guard_type);
                if (err != null)
                    return "composition: " + err;
            }
            return null;
        }

        private string AnalyseGuard(Expression guard, out Atomic type)
        {
            Expression exp;
            type = Atomic.UNDEFINED;

            if (guard is ParensExpression)
                exp = ((ParensExpression)guard).E;
            else
                exp = guard;

            if (exp is ApplySuffix)
                type = GetStatementType((ApplySuffix)exp);
            else
                return "Invalid composition guard; Expected atomic statement; Received " + exp.GetType();

            return null;
        }
    }
}
