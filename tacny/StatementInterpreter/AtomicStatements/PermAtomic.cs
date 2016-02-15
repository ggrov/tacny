using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System;

namespace Tacny
{
    class PermAtomic : Atomic, IAtomicStmt
    {
        public PermAtomic(Atomic atomic) : base(atomic) { }

        // Holds the result of each  perm()
        private List<UpdateStmt> solutions = new List<UpdateStmt>();

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            // generate all possible member calls
            GenPermutations(st);
            
            // generate all the possible member combinations
            PermuteResults(ref solution_list);
        }

        /// <summary>
        /// Called to recursively resolve permutations
        /// </summary>
        /// <param name="st">Statement to analyse</param>
        /// <returns></returns>
        private void GenPermutations(Statement st)
        {
            List<UpdateStmt> sol = new List<UpdateStmt>();
            // generate permutations
            Perm(st, ref sol);
            solutions.AddRange(sol);
        }

        /// <summary>
        /// Generate all possible combinations of method calls
        /// </summary>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private void PermuteResults(ref List<Solution> solution_list)
        {
            // generate the solutions
            foreach (var item in solutions)
            {
                Atomic ac = this.Copy();
                // create a deep copy of each UpdateStmt
                UpdateStmt nus = Util.Copy.CopyUpdateStmt(item);
                ac.AddUpdated(nus, nus);
                solution_list.Add(new Solution(ac));
            }
        }

        /// <summary>
        /// Generate all combinations from one permutation
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private void Perm(Statement st, ref List<UpdateStmt> solution_list)
        {
            List<List<NameSegment>> result = new List<List<NameSegment>>(); // a combination of all possible variable combinations
            List<List<IVariable>> args = new List<List<IVariable>>();
            List<IVariable> md_ins = new List<IVariable>();
            List<Expression> call_arguments = null;
            object member;
            MemberDecl md;
            InitArgs(st, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));
                     
            ProcessArg(call_arguments[0], out member);
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

            object ovars;
            ProcessArg(call_arguments[1], out ovars);
            Contract.Assert(ovars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

            List<IVariable> vars = ovars as List<IVariable>;
            Contract.Assert(vars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

            
            
            for (int i = 0; i < md_ins.Count; i++)
            {
                var item = md_ins[i];
                args.Add(new List<IVariable>());
                foreach (var arg in vars)
                {
                    // get variable type
                    Dafny.Type type = globalContext.GetVariableType(arg.Name);
                    if (type != null)
                    {
                        // if variable type and current argument types match
                        if (item.Type.ToString() == type.ToString())
                            args[i].Add(arg);
                    } else
                        args[i].Add(arg);
                }
                /**
                 * if no type correct variables have been added we can safely return
                 * because we won't be able to generate valid calls
                 */
                if (args[i].Count == 0)
                    return;
            }

            PermuteArguments(args, 0, new List<NameSegment>(), ref result);

            if (result.Count == 0)
            {
                ApplySuffix aps = new ApplySuffix(call_arguments[0].tok, new NameSegment(call_arguments[0].tok, md.Name, null), new List<Expression>());
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                Console.WriteLine(Dafny.Printer.StatementToString(us));
                solution_list.Add(us);
            }
            else
            {
                foreach (var item in result)
                {

                    // create new fresh list of items to remove multiple references to the same object
                    List<Expression> new_list = Util.Copy.CopyExpressionList(item.Cast<Expression>().ToList());
                    ApplySuffix aps = new ApplySuffix(call_arguments[0].tok, new NameSegment(call_arguments[0].tok, md.Name, null), new_list);
                    UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                    solution_list.Add(us);
                }
            }
        }

        private void PermuteArguments(List<List<IVariable>> args, int depth, List<NameSegment> current, ref List<List<NameSegment>> result)
        {
            if (args.Count == 0) return;
            if (depth == args.Count)
            {
                result.Add(current);
                return;
            }
            for (int i = 0; i < args[depth].Count; ++i)
            {
                List<NameSegment> tmp = new List<NameSegment>();
                tmp.AddRange(current);
                IVariable iv = args[depth][i];
                NameSegment ns = new NameSegment(iv.Tok, iv.Name, null);
                tmp.Add(ns);
                PermuteArguments(args, depth + 1, tmp, ref result);
            }
        }
    }
}
