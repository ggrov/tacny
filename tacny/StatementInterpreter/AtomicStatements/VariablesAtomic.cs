using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using System.Diagnostics.Contracts;

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
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> locals = new List<IVariable>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            Method source = localContext.md as Method;
            Contract.Assert(source != null, Util.Error.MkErr(st, 4));

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
