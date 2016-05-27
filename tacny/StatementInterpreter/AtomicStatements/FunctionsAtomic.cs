using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

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
            List<MemberDecl> functions = new List<MemberDecl>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));

            foreach (var member in globalContext.program.Members.Values)
            {
                Function fun = member as Function;
                if (fun != null)
                    functions.Add(fun);
            }
            AddLocal(lv, functions);
            solution_list.Add(new Solution(Copy()));
        }
    }
}
