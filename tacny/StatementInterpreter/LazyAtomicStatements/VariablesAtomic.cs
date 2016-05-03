using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny
{
    class VariablesAtomic : Atomic, IAtomicLazyStmt
    {
        public VariablesAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            yield return GetVariables(st, solution);
            yield break;
        }

        private Solution GetVariables(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments;
            List<IVariable> locals = new List<IVariable>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            Method source = DynamicContext.md as Method;
            Contract.Assert(source != null, Util.Error.MkErr(st, 4));

            locals.AddRange(StaticContext.staticVariables.Values.ToList());
        
            AddLocal(lv, locals);
            return new Solution(this.Copy());
        }

       
    }
}
