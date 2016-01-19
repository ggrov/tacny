using System;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class MergeAtomic : Atomic, IAtomicStmt
    {
        public MergeAtomic(Atomic atomic) : base(atomic) { }
        /// <summary>
        /// Merge two lists together
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Merge(st, ref solution_list);
        }


        private void Merge(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));

            object arg1;
            ProcessArg(call_arguments[0], out arg1);
            object arg2;
            ProcessArg(call_arguments[1], out arg2);
            dynamic darg1 = arg1;
            dynamic darg2 = arg2;

            if (!darg1.GetType().Equals(darg2.GetType()) && !(darg1 is IEnumerable))
                Contract.Assert(false, Util.Error.MkErr(st, 1, typeof(List<IVariable>)));
            

            darg1.AddRange(darg2);
            AddLocal(lv, darg1);

            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
