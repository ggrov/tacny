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
        private List<List<UpdateStmt>> solutions = new List<List<UpdateStmt>>();

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            // generate all possible member calls
            string err = GenPermutations(st);
            if (err != null)
                return err;
            // generate all the possible member combinations
            PermuteResults(ref solution_list);
                return null;

        }

        /// <summary>
        /// Called to recursively resolve permutations
        /// </summary>
        /// <param name="st">Statement to analyse</param>
        /// <returns></returns>
        private string GenPermutations(Statement st)
        {
            string err;
            List<UpdateStmt> sol = new List<UpdateStmt>();
            // generate permutations
            err = Perm(st, ref sol);
            if (err != null)
                return err;
            solutions.Add(sol);
            Statement nextStatement = localContext.GetNextStatement();
            if (StatementRegister.GetAtomicType(nextStatement) == StatementRegister.Atomic.PERM)
            {
                localContext.IncCounter();
                err = GenPermutations(nextStatement);
                if (err != null)
                    return err;
                return null;

            }

            return null;
        }

        /// <summary>
        /// Generate all possible combinations of method calls
        /// </summary>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private string PermuteResults(ref List<Solution> solution_list)
        {
            List<List<UpdateStmt>> result = new List<List<UpdateStmt>>();
            //foreach (var tmp in solutions[0])
              //  result.Add(new List<UpdateStmt>() { tmp });
            GenerateMethodPremutations(solutions, 0, new List<UpdateStmt>(), ref result);
            // generate the solutions
            foreach (var item in result)
            {
                IncTotalBranchCount();
                Atomic ac = this.Copy();
                // create a deep copy of each UpdateStmt
                foreach (var us in item)
                {
                    UpdateStmt nus = Util.Copy.CopyUpdateStmt(us);
                    ac.AddUpdated(nus, nus);
                }

                solution_list.Add(new Solution(ac));
            }
            return null;
        }

        /// <summary>
        /// Generate all combinations from one permutation
        /// </summary>
        /// <param name="st"></param>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private string Perm(Statement st, ref List<UpdateStmt> solution_list)
        {

            List<List<NameSegment>> result = new List<List<NameSegment>>(); // a combination of all possible variable combinations
            List<List<IVariable>> args = new List<List<IVariable>>();
            List<Expression> call_arguments = null;
            MemberDecl md;
            string err;
            Method m;

            err = InitArgs(st, out call_arguments);
            if (err != null) return err;
            if (call_arguments.Count != 2)
                return String.Format("Wrong number of method arguments. Expected 2 received {0}", call_arguments.Count);

            object member;
            err = ProcessArg(call_arguments[0], out member);
            if (err != null)
                return err;
            md = member as MemberDecl;
            if (md == null)
                return String.Format("Member {0} is not defined", call_arguments[0]);

            object ovars;
            err = ProcessArg(call_arguments[1], out ovars);
            List<IVariable> vars = ovars as List<IVariable>;
            if (vars == null)
                return String.Format("Unexpected list of arguments");

            m = md as Method;
            if (m == null)
                return String.Format("Member declaration {0} not found", call_arguments[0]);

            int i = 0;
            foreach (var item in m.Ins)
            {
                args.Add(new List<IVariable>());
                foreach (var arg in vars)
                {
                    Dafny.LocalVariable lv;
                    if (arg is Dafny.LocalVariable)
                    {
                        lv = arg as Dafny.LocalVariable;
                        Dafny.InferredTypeProxy itp = lv.OptionalType as InferredTypeProxy;
                        if (itp != null)
                        {
                            if (itp.T == null)
                                args[i].Add(arg);
                        }
                        else
                        {
                            if (item.Type.Equals(lv.OptionalType))
                            {
                                args[i].Add(arg);
                            }
                        }

                    }

                    else if (item.Type.Equals(arg.Type))
                        args[i].Add(arg);
                }

                i++;
            }

            GeneratePremutations(args, 0, new List<NameSegment>(), ref result);

            if (result.Count == 0)
            {
                ApplySuffix aps = new ApplySuffix(call_arguments[0].tok, new NameSegment(call_arguments[0].tok, md.Name, null), new List<Expression>());
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                solution_list.Add(us);
            }
            else
            {
                foreach (var item in result)
                {

                    // create new fresh list of items to remove multiple references to the same object
                    List<Expression> new_list = GenerateNew(item);
                    ApplySuffix aps = new ApplySuffix(call_arguments[0].tok, new NameSegment(call_arguments[0].tok, md.Name, null), new_list);
                    UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                    solution_list.Add(us);
                }
            }
            return null;
        }

        private string FindMd(Expression md_signature, out MemberDecl md)
        {
            md = null;
            NameSegment ns = md_signature as NameSegment;
            if (ns == null) return String.Format("Unsupported argument. Expected NameSegment received {0}", md_signature.GetType());
            md = program.GetTactic(ns.Name);
            if (md != null) return null;
            md = program.GetMember(ns.Name);
            if (md != null) return null;

            return String.Format("Member {0} is not defined", ns.Name);
        }

        private void GenerateMethodPremutations(List<List<UpdateStmt>> methods, int depth, List<UpdateStmt> current, ref List<List<UpdateStmt>> result)
        {
            if (methods.Count == 0) return;
            if (depth == methods.Count)
            {
                result.Add(current);
                return;
            }

            for (int i = 0; i < methods[depth].Count; ++i)
            {
                List<UpdateStmt> tmp = new List<UpdateStmt>();
                tmp.AddRange(current);
                tmp.Add(methods[depth][i]);
                GenerateMethodPremutations(methods, depth + 1, tmp, ref result);
            }
        }

        private void GeneratePremutations(List<List<IVariable>> args, int depth, List<NameSegment> current, ref List<List<NameSegment>> result)
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
                GeneratePremutations(args, depth + 1, tmp, ref result);
            }
        }


        private List<Expression> GenerateNew(List<NameSegment> old_list)
        {
            List<Expression> new_list = new List<Expression>();

            foreach (var item in old_list)
            {
                new_list.Add(new NameSegment(item.tok, item.Name, item.OptTypeArguments));
            }

            return new_list;
        }
    }
}
