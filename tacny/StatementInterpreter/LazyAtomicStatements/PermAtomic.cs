using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Tacny;
using Util;
using Type = Microsoft.Dafny.Type;

namespace LazyTacny {
  class PermAtomic : Atomic, IAtomicLazyStmt {
    

    public PermAtomic(Atomic atomic) : base(atomic)
    {
      
    }


    public IEnumerable<Solution> Resolve(Statement st, Solution solution)
    {
      return Perm(st);
    }

    /// <summary>
    /// Generate all combinations from one permutation
    /// </summary>
    /// <param name="st"></param>
    /// <returns></returns>
    private IEnumerable<Solution> Perm(Statement st) {
      List<List<IVariable>> args = new List<List<IVariable>>();
      List<IVariable> mdIns = new List<IVariable>();
      List<Expression> callArguments;
      IVariable lv;
      InitArgs(st, out lv, out callArguments);
      Contract.Assert(tcce.OfSize(callArguments, 2), Error.MkErr(st, 0, 2, callArguments.Count));

      foreach (var member in ResolveExpression(callArguments[0])) {
        Contract.Assert(member != null, Error.MkErr(callArguments[0], 1, typeof(Method)));
        MemberDecl md;
        if (member is NameSegment) {
          md = StaticContext.program.Members.FirstOrDefault(i => i.Key == (member as NameSegment)?.Name).Value;
        } else {
          md = member as MemberDecl;
        }
        Contract.Assert(md != null, Error.MkErr(callArguments[0], 1, typeof(MemberDecl)));

        // take the membed decl parameters
        var method = md as Method;
        if (method != null)
          mdIns.AddRange(method.Ins);
        else if (md is Function)
          mdIns.AddRange(((Function)md).Formals);
        else
          Contract.Assert(false, Error.MkErr(callArguments[0], 1, $"{typeof(Method)} or {typeof(Function)}"));

        foreach (var ovars in ResolveExpression(callArguments[1])) {
          Contract.Assert(ovars != null, Error.MkErr(callArguments[0], 1, typeof(List<IVariable>)));

          List<IVariable> vars = ovars as List<IVariable> ?? new List<IVariable>();
          //Contract.Assert(vars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));



          for (int i = 0; i < mdIns.Count; i++) {
            var item = mdIns[i];
            args.Add(new List<IVariable>());
            foreach (var arg in vars) {
              // get variable type
              Type type = StaticContext.GetVariableType(arg.Name);
              if (type != null) {
                if (type is UserDefinedType && item.Type is UserDefinedType) {
                  var udt1 = type as UserDefinedType;
                  var udt2 = item.Type as UserDefinedType;
                  if (udt1.Name == udt2.Name)
                    args[i].Add(arg);
                } else {
                  // if variable type and current argument types match
                  if (item.Type.ToString() == type.ToString())
                    args[i].Add(arg);
                }
              } else
                args[i].Add(arg);
            }
            /**
             * if no type correct variables have been added we can safely return
             * because we won't be able to generate valid calls
             */
            if (args[i].Count == 0) {
              Debug.WriteLine("No type matching variables were found");
              yield break;
            }
          }

          foreach (var result in PermuteArguments(args, 0, new List<NameSegment>())) {

            // create new fresh list of items to remove multiple references to the same object
            List<Expression> newList = Util.Copy.CopyExpressionList(result.Cast<Expression>().ToList());
            ApplySuffix aps = new ApplySuffix(callArguments[0].tok, new NameSegment(callArguments[0].tok, md.Name, null), newList);
            if (lv != null)
              yield return AddNewLocal(lv, aps);
            else {
              UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
              yield return AddNewStatement(us, us);
            }
          }
        }
      }
    }

    private static IEnumerable<List<NameSegment>> PermuteArguments(List<List<IVariable>> args, int depth, List<NameSegment> current) {
      if (args.Count == 0) yield break;
      if (depth == args.Count) {
        yield return current;
        yield break;
      }
      for (int i = 0; i < args[depth].Count; ++i) {
        List<NameSegment> tmp = new List<NameSegment>();
        tmp.AddRange(current);
        IVariable iv = args[depth][i];
        NameSegment ns = new NameSegment(iv.Tok, iv.Name, null);
        tmp.Add(ns);
        foreach (var item in PermuteArguments(args, depth + 1, tmp))
          yield return item;
      }
    }
  }
}
