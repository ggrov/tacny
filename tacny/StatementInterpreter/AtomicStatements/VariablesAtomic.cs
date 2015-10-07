using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class VariablesAtomic : Atomic, IAtomicStmt
    {
        public VariablesAtomic(Atomic atomic) : base(atomic) { }
        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return GetVariables(st, ref solution_list);
        }


        private string GetVariables(Statement st, ref List<Solution> solution_list)
        {
            string err;
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> locals = new List<IVariable>();

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "ERROR variables: " + err;

            if (call_arguments.Count != 0)
                return "ERROR variables:  the call does not take any arguments";

            Method source = localContext.md as Method;
            if(source == null)
                return "ERROR variables: unexpected source method type: expected method received " + localContext.md.GetType();
            foreach (var stmt in source.Body.Body)
            {
                VarDeclStmt vds = null;
                if((vds = stmt as VarDeclStmt) != null)
                    locals.AddRange(vds.Locals);

                if (stmt.Equals(localContext.tac_call))
                    break;
            }

            if (globalContext.temp_variables.Count > 0)

                locals.AddRange(globalContext.temp_variables.Values.ToList());
            IncTotalBranchCount();
            AddLocal(lv, locals);
            solution_list.Add(new Solution(this.Copy()));

            return null;
        }
    }
}
