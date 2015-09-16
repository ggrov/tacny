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

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "ERROR variables: " + err;

            if (call_arguments.Count != 0)
                return "ERROR variables: unexpected arguments";

            AddLocal(lv, new List<IVariable>(globalContext.global_variables.Values.ToArray()));
            solution_list.Add(new Solution(this.Copy()));

            return null;
        }
    }
}
