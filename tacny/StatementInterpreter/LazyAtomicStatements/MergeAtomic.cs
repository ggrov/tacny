using System;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using Tacny;
using System.Diagnostics;
namespace LazyTacny
{
    class MergeAtomic : Atomic, IAtomicLazyStmt
    {
        public MergeAtomic(Atomic atomic) : base(atomic) { }
        /// <summary>
        /// Merge two lists together
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            
            IVariable lv = null;
            List<Expression> call_arguments;
            IList result = null;
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));

            foreach (var arg1 in ProcessStmtArgument(call_arguments[0]))
            {
                foreach (var arg2 in ProcessStmtArgument(call_arguments[1]))
                {
                    // valdiate the argument types
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
                    yield return new Solution(this.Copy());
                }
            }
            
            
            yield break;
        }

    }
}
