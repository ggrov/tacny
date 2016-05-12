using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System;
using System.Diagnostics;
using Tacny;


namespace LazyTacny {
  class PermAtomic : Atomic, IAtomicLazyStmt {
    public PermAtomic(Atomic atomic) : base(atomic) { }

    // Holds the result of each  perm()
    private List<UpdateStmt> solutions = new List<UpdateStmt>();

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      foreach (var item in Perm(st, solution))
        yield return item;

      yield break;
    }

    /// <summary>
    /// Generate all combinations from one permutation
    /// </summary>
    /// <param name="st"></param>
    /// <param name="solution_list"></param>
    /// <returns></returns>
    private IEnumerable<Solution> Perm(Statement st, Solution sol) {
      List<List<IVariable>> args = new List<List<IVariable>>();
      List<IVariable> md_ins = new List<IVariable>();
      List<Expression> call_arguments = null;
      MemberDecl md;
      InitArgs(st, out call_arguments);
      Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));

      foreach (var member in ProcessStmtArgument(call_arguments[0])) {
        Contract.Assert(member != null, Util.Error.MkErr(call_arguments[0], 1, typeof(Method)));

        md = member as MemberDecl;
        Contract.Assert(md != null, Util.Error.MkErr(call_arguments[0], 1, typeof(MemberDecl)));

        // take the membed decl parameters
        if (member is Method)
          md_ins.AddRange(((Method)member).Ins);
        else if (member is Dafny.Function)
          md_ins.AddRange(((Dafny.Function)member).Formals);
        else
          Contract.Assert(false, Util.Error.MkErr(call_arguments[0], 1, String.Format("{0} or {1}", typeof(Method), typeof(Dafny.Function))));

        foreach (var ovars in ProcessStmtArgument(call_arguments[1])) {
          Contract.Assert(ovars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

          List<IVariable> vars = ovars as List<IVariable>;
          Contract.Assert(vars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));



          for (int i = 0; i < md_ins.Count; i++) {
            var item = md_ins[i];
            args.Add(new List<IVariable>());
            foreach (var arg in vars) {
              // get variable type
              Dafny.Type type = StaticContext.GetVariableType(arg.Name);
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
            List<Expression> new_list = Util.Copy.CopyExpressionList(result.Cast<Expression>().ToList());
            ApplySuffix aps = new ApplySuffix(call_arguments[0].tok, new NameSegment(call_arguments[0].tok, md.Name, null), new_list);
            UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
            Atomic ac = this.Copy();
            ac.AddUpdated(us, us);
            Solution.PrintSolution(new Solution(ac));
            yield return new Solution(ac);
          }
        }
      }

      yield break;
    }

    private IEnumerable<List<NameSegment>> PermuteArguments(List<List<IVariable>> args, int depth, List<NameSegment> current) {
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
      yield break;
    }
  }
}
