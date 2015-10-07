using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using Dafny = Microsoft.Dafny;
using Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class MergeAtomic : Atomic, IAtomicStmt
    {
        public MergeAtomic(Atomic atomic)
            : base(atomic)
        {

        }
        /// <summary>
        /// Merge two lists together
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Merge(st, ref solution_list);
        }


        private string Merge(Statement st, ref List<Solution> solution_list)
        {
            string err;
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this

            err = InitArgs(st, out lv, out call_arguments);

            if (lv == null)
                return String.Format("merge: unexpected number of result arguments");

            if (call_arguments.Count != 2)
                return String.Format("merge: Unexpected number of input arguments expected 2 received {0}", call_arguments.Count);

            object arg1;
            err = ProcessArg(call_arguments[0], out arg1);
            if (err != null)
                return err;
            object arg2;
            err = ProcessArg(call_arguments[1], out arg2);
            if (err != null)
                return err;
            dynamic darg1 = arg1;
            dynamic darg2 = arg2;

            if (!darg1.GetType().Equals(darg2.GetType()) || !(darg1 is IEnumerable))
                return String.Format("merge: List types do not match. Arg1: {0} Arg2: {1}", darg1.GetType(), darg2.GetType());


            darg1.AddRange(darg2);
            IncTotalBranchCount();
            AddLocal(lv, darg1);

            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
