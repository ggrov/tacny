using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class ReturnAtomic : Atomic, IAtomicStmt
    {
        public ReturnAtomic(Atomic atomic) : base(atomic) { }



        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Returns(st, ref solution_list);
        }


        private void Returns(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> input = new List<IVariable>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));
            Contract.Assert(lv != null, Error.MkErr(st, 8));

            Method source = localContext.md as Method;
            Contract.Assert(source != null, Error.MkErr(st, 4));

            input.AddRange(source.Ins);
            AddLocal(lv, input);
            solution_list.Add(new Solution(Copy()));

            
        }
    }
}
