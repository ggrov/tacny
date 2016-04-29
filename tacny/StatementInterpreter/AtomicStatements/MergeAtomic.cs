﻿using System;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
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
            IList result = null;
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));

            object arg1;
            ProcessArg(call_arguments[0], out arg1);
            object arg2;
            ProcessArg(call_arguments[1], out arg2);
            System.Type type1 = arg1.GetType().GetGenericArguments().Single();
            System.Type type2 = arg2.GetType().GetGenericArguments().Single();
            Contract.Assert(type1.Equals(type2), Util.Error.MkErr(st, 1, type1));

            if (!(arg1 is IEnumerable) || !(arg2 is IEnumerable))
                Contract.Assert(false, Util.Error.MkErr(st, 1, typeof(IEnumerable)));

            dynamic darg1 = arg1;
            dynamic darg2 = arg2;

            System.Type listType = typeof(List<>).MakeGenericType(new[] { type1 });
            result = (IList)Activator.CreateInstance(listType);


            foreach (var t in darg1)
            {
                if (!result.Contains(t))
                    result.Add(t);
            }

            foreach (var t in darg2)
            {
                if (!result.Contains(t))
                    result.Add(t);
            }
            AddLocal(lv, result);

            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
