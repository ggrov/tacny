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
            
            solutions.Add(sol);
            Statement nextStatement = localContext.GetNextStatement();
            if (nextStatement != null && StatementRegister.GetAtomicType(nextStatement) == StatementRegister.Atomic.PERM)
            {
                localContext.IncCounter();
                GenPermutations(nextStatement);
            }
        }

        /// <summary>
        /// Generate all possible combinations of method calls
        /// </summary>
        /// <param name="solution_list"></param>
        /// <returns></returns>
        private void PermuteResults(ref List<Solution> solution_list)
        {
            List<List<UpdateStmt>> result = new List<List<UpdateStmt>>();
            //foreach (var tmp in solutions[0])
              //  result.Add(new List<UpdateStmt>() { tmp });
            GenerateMethodPremutations(solutions, 0, new List<UpdateStmt>(), ref result);
            // generate the solutions
            foreach (var item in result)
            {
                Atomic ac = this.Copy();
                // create a deep copy of each UpdateStmt
                foreach (var us in item)
                {
                    UpdateStmt nus = Util.Copy.CopyUpdateStmt(us);
                    ac.AddUpdated(nus, nus);
                }

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
            List<Expression> call_arguments = null;
            object member;
            Method md;   

            InitArgs(st, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 2), Util.Error.MkErr(st, 0, 2, call_arguments.Count));
                     
            ProcessArg(call_arguments[0], out member);
            Contract.Assert(member != null, Util.Error.MkErr(call_arguments[0], 1, typeof(Method)));

            md = member as Method;
            Contract.Assert(md != null, Util.Error.MkErr(call_arguments[0], 1, typeof(Method)));

            object ovars;
            ProcessArg(call_arguments[1], out ovars);
            Contract.Assert(ovars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

            List<IVariable> vars = ovars as List<IVariable>;
            Contract.Assert(vars != null, Util.Error.MkErr(call_arguments[0], 1, typeof(List<IVariable>)));

            
            int i = 0;
            foreach (var item in md.Ins)
            {
                args.Add(new List<IVariable>());
                foreach (var arg in vars)
                {
                    Dafny.LocalVariable lv;
                    Dafny.Type type = globalContext.GetVariableType(arg.Name);
                    
                    if (item.Type.ToString() == type.ToString())
                        args[i].Add(arg);
                    //if (arg is dafny.localvariable)
                    //{
                    //    lv = arg as dafny.localvariable;
                    //    dafny.type type = globalcontext.getvariabletype(lv.name);
                    //    //dafny.inferredtypeproxy itp = lv.optionaltype as inferredtypeproxy;
                    //    //if (itp != null)
                    //    //{
                    //    //    if (itp.t == null)
                    //    //        args[i].add(arg);
                    //    //}
                    //    //else
                    //    //{
                    //    //    if (item.type.equals(lv.optionaltype))
                    //    //    {
                    //    //        args[i].add(arg);
                    //    //    }
                    //    //}

                    //}

                    //else if (item.type.equals(arg.type))
                    //    args[i].add(arg);
                }
                /**
                 * if no type correct variables have been added we can safely return
                 * because we won't be able to generate valid calls
                 */
                if (args[i].Count == 0)
                    return;

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
                new_list.Add(new NameSegment(item.tok, item.Name, null));
            }

            return new_list;
        }
    }
}
