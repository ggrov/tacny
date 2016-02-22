using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny
{
    class ParamsAtomic : Atomic, IAtomicLazyStmt
    {
        public ParamsAtomic(Atomic atomic) : base(atomic) { }



        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            yield return Params(st, solution);
            yield break;
        }


        private Solution Params(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> input = new List<IVariable>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            Method source = localContext.md as Method;
            Contract.Assert(source != null, Util.Error.MkErr(st, 4));

            input.AddRange(source.Ins);
            //input.AddRange(source.Outs);
            AddLocal(lv, input);
            return new Solution(this.Copy());
        }
    }
}
