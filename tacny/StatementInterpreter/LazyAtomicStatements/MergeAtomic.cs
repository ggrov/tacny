﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Tacny;
using Util;
using Type = System.Type;

namespace LazyTacny {
  class MergeAtomic : Atomic, IAtomicLazyStmt {
    public MergeAtomic(Atomic atomic) : base(atomic) { }
    /// <summary>
    /// Merge two lists together
    /// </summary>
    /// <param name="st"></param>
    /// <param name="solution_list"></param>
    /// <returns></returns>
    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      IVariable lv = null;
      List<Expression> call_arguments;
      IList result = null;
      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(lv != null, Error.MkErr(st, 8));
      Contract.Assert(tcce.OfSize(call_arguments, 2), Error.MkErr(st, 0, 2, call_arguments.Count));

      foreach (var arg1 in ResolveExpression(call_arguments[0])) {
        foreach (var arg2 in ResolveExpression(call_arguments[1])) {
          // valdiate the argument types
          Type type1 = arg1.GetType().GetGenericArguments().Single();
          Type type2 = arg2.GetType().GetGenericArguments().Single();
          Contract.Assert(type1.Equals(type2), Error.MkErr(st, 1, type1));

          if (!(arg1 is IEnumerable) || !(arg2 is IEnumerable))
            Contract.Assert(false, Error.MkErr(st, 1, typeof(IEnumerable)));

          dynamic darg1 = arg1;
          dynamic darg2 = arg2;

          result = MergeLists(darg1, darg2, type1);
          yield return AddNewLocal(lv, result);
        }
      }
    }


    private static IList  MergeLists(dynamic l1, dynamic l2, Type type) {
      Type listType = typeof(List<>).MakeGenericType(type);
      var result = (IList)Activator.CreateInstance(listType);
      foreach (var t in l1) {
        result.Add(t);
      }

      foreach (var t in l2) {
        if (type == typeof(IVariable)) {
          if (result.Cast<IVariable>().Count(x => x.Name == t.Name) == 0)
            result.Add(t);
        } else {
          result.Add(t);
        }
      }

      return result;
    }

  }
}
