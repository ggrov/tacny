using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
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
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));

            Method source = localContext.md as Method;
            Contract.Assert(source != null, Util.Error.MkErr(st, 4));

            input.AddRange(source.Ins);
            AddLocal(lv, input);
            solution_list.Add(new Solution(this.Copy()));

            
        }
    }
}
