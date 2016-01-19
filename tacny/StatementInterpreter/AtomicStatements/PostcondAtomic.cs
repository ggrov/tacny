using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class PostcondAtomic : Atomic, IAtomicStmt
    {
        public PostcondAtomic(Atomic atomic) : base(atomic) { }



        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Postcond(st, ref solution_list);
        }

        private void Postcond(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<MaybeFreeExpression> post_conditions = new List<MaybeFreeExpression>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));

            Method method = globalContext.md as Method;
            Contract.Assert(method != null, Util.Error.MkErr(st, 15, "postconditions"));
            foreach (var post in method.Ens)
            {
                post_conditions.Add(Util.Copy.CopyMaybeFreeExpression(post));

            }
            AddLocal(lv, post_conditions);
            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
