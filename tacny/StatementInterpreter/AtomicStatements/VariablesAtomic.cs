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

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            GetVariables(st, ref solution_list);
        }


        private void GetVariables(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> locals = new List<IVariable>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            Method source = localContext.md as Method;
            Contract.Assert(source != null, Util.Error.MkErr(st, 4));

            locals.AddRange(globalContext.staticVariables.Values.ToList());
        
            AddLocal(lv, locals);
            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
