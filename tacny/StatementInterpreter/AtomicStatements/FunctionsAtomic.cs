using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class FunctionsAtomic : Atomic, IAtomicStmt
    {
        public FunctionsAtomic(Atomic atomic) : base(atomic) { }



        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Functions(st, ref solution_list);
        }

        private void Functions(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<Function> functions = new List<Function>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            foreach (var member in program.members.Values)
            {
                Function fun = member as Function;
                if (fun != null)
                    functions.Add(fun);
            }
            IncTotalBranchCount();
            AddLocal(lv, functions);
            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
