using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class ParamsAtomic : Atomic, IAtomicStmt
    {
        public ParamsAtomic(Atomic atomic) : base(atomic) { }



        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Params(st, ref solution_list);
        }


        private string Params(Statement st, ref List<Solution> solution_list)
        {
            string err;
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<IVariable> input = new List<IVariable>();

            err = InitArgs(st, out lv, out call_arguments);
            if (err != null)
                return "ERROR params: " + err;

            if (call_arguments.Count != 0)
                return "ERROR params: the call does not take any arguments";

            Method source = localContext.md as Method;
            if (source == null)
                return "ERROR params: unexpected source method type: expected method received " + localContext.md.GetType();

            input.AddRange(source.Ins);

            AddLocal(lv, input);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
