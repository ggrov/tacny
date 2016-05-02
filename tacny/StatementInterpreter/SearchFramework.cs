﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace LazyTacny
{
    [ContractClass(typeof(ISearchContract))]
    public interface ISearch
    {
        IEnumerable<LazyTacny.Solution> Search(Atomic atomic, bool verify = true);
        //  IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic ac);
    }
    public enum Strategy
    {
        BFS = 0,
        DFS
    }
    [ContractClassFor(typeof(ISearch))]
    // Validate the input before execution
    public abstract class ISearchContract : ISearch
    {
        public IEnumerable<Solution> Search(Atomic atomic, bool verify = true)
        {
            Contract.Requires(atomic != null);

            yield break;
        }
    }

    public class SearchStrategy : ISearch
    {
       private Strategy ActiveStrategy = Strategy.BFS;
        public SearchStrategy(Strategy strategy)
        {
            if (Util.TacnyOptions.O.EnableSearch >= 0)
            {
                try
                {
                    ActiveStrategy = (Strategy)Util.TacnyOptions.O.EnableSearch;
                }
                catch
                {
                    ActiveStrategy = Strategy.BFS;
                }
            }
            else
                ActiveStrategy = strategy;

        }

        protected SearchStrategy()
        {

        }

        public IEnumerable<Solution> Search(Atomic atomic, bool verify = true)
        {
            IEnumerable<Solution> enumerable;
            switch (ActiveStrategy)
            {
                case Strategy.BFS:
                    enumerable = BreadthFirstSeach.Search(atomic, verify);
                    break;
                case Strategy.DFS:
                    enumerable = DepthFirstSeach.Search(atomic, verify);
                    break;
                default:
                    enumerable = BreadthFirstSeach.Search(atomic, verify);
                    break;
            }
            // return a fresh copy of the atomic
            foreach (var item in enumerable)
                yield return new Solution(item.state.Copy());
            yield break;
        }

        public  static Strategy GetSearchStrategy(Tactic tac)
        {
            Contract.Requires<ArgumentNullException>(tac != null);
            Attributes attrs = tac.Attributes;
            if (attrs != null)
            {
                if (attrs.Name == "search")
                {
                    Expression expr = attrs.Args.FirstOrDefault();
                    if (expr != null)
                    {
                        // the search strategy is expected to be a name segment
                        var ns = expr as NameSegment;
                        if (ns != null)
                        {
                            switch (ns.Name.ToUpper())
                            {
                                case "BFS":
                                    return Strategy.BFS;
                                case "DFS":
                                    return Strategy.DFS;
                                default:
                                    Contract.Assert(false, (Util.Error.MkErr(expr, 19, ns.Name)));
                                    return Strategy.BFS;

                            }
                        }
                    }
                }
            }
            // default search strategy  
            return Strategy.BFS;
        }


        internal static bool VerifySolution(Solution solution)
        {
            if (!solution.state.staticContext.program.HasError())
            {
                // return the valid solution and terminate
                return true;
            }
            else
            {  // if verifies break else continue to the next solution
                if (solution.state.ResolveAndVerify(solution))
                {
                    return solution.state.staticContext.program.HasError() ? false : true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    internal class BreadthFirstSeach : SearchStrategy
    {
        public static new IEnumerable<Solution> Search(Atomic atomic, bool verify = true)
        {
            Debug.WriteLine(String.Format("Resolving tactic {0}", atomic.dynamicContext.tactic));

            //local solution list
            List<Solution> result = atomic == null ? new List<Solution>() : new List<Solution>() { new Solution(atomic) };

            while (true)
            {
                List<Solution> Interm = new List<Solution>();
                if (result.Count == 0)
                {

                    yield break;
                }
                // iterate every solution
                foreach (var item in result)
                {
                    // lazily resolve a statement in the solution
                    foreach (var solution in Atomic.ResolveStatement(item))
                    {
                        // validate result
                        if (solution.IsResolved())
                        {
                            if (verify)
                            {
                                if (VerifySolution(solution)) { yield return solution; yield break; } else { continue; }
                            }
                            else { yield return solution; }
                        }
                        else if (solution.state.dynamicContext.isPartialyResolved)
                        {
                            if (verify)
                            {
                                if (VerifySolution(solution)) { yield return solution; yield break; }
                                else { Interm.Add(solution); continue; }
                            }
                            else { Interm.Add(solution); yield return solution; }
                        }
                        else
                        {
                            Interm.Add(solution);
                        }
                    }
                }
                result.Clear();
                result.AddRange(Interm);
            }
        }

        public static new IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic)
        {
            Atomic ac = atomic.Copy();
            ac.dynamicContext.tacticBody = body.Body;
            ac.dynamicContext.ResetCounter();
            List<Solution> result = new List<Solution>() { new Solution(ac) };
            // search strategy for body goes here
            while (true)
            {
                List<Solution> interm = new List<Solution>();
                if (result.Count == 0)
                    break;
                foreach (var solution in result)
                {
                    foreach (var item in Atomic.ResolveStatement(solution))
                    {
                        if (item.state.dynamicContext.isPartialyResolved)
                        {
                            { interm.Add(item); }
                            yield return item;
                        }
                        else if (item.state.dynamicContext.GetCurrentStatement() == null) { yield return item; }
                        else { interm.Add(item); }
                    }
                }

                result.Clear();
                result.AddRange(interm);
            }
        }
    }

    internal class DepthFirstSeach : SearchStrategy
    {
        public static new IEnumerable<Solution> Search(Atomic atomic, bool verify = true)
        {

            Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();

            if (atomic != null)
                solutionStack.Push(Atomic.ResolveStatement(new Solution(atomic)).GetEnumerator());

            while (true)
            {
                if (solutionStack.Count == 0)
                {

                    yield break;
                }

                var solutionEnum = solutionStack.Pop();

                // if the solution is fully resolved skip resolution
                if (!solutionEnum.MoveNext())
                    continue;

                var solution = solutionEnum.Current;
                solutionStack.Push(solutionEnum);
                if (solution.IsResolved())
                {
                    if (verify)
                    {
                        if (VerifySolution(solution)) { yield return solution; yield break; } else { continue; }
                    }
                    else
                    {
                        yield return solution;
                    }
                }
                else if (solution.state.dynamicContext.isPartialyResolved)
                {
                    if (verify)
                    {
                        if (VerifySolution(solution)) { yield return solution; yield break; }
                        else
                        {
                            solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
                            continue;
                        }
                    }
                    else {
                        solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
                        yield return solution;
                    }
                }
                else
                {
                    solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
                }



            }

        }

        public static new IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic)
        {
            Atomic ac = atomic.Copy();
            ac.dynamicContext.tacticBody = body.Body;
            ac.dynamicContext.ResetCounter();
            Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();
            solutionStack.Push(Atomic.ResolveStatement(new Solution(ac)).GetEnumerator());

            while (true)
            {
                if (solutionStack.Count == 0)
                {

                    yield break;
                }

                var solutionEnum = solutionStack.Pop();

                // if the solution is fully resolved skip resolution
                if (!solutionEnum.MoveNext())
                    continue;
                var solution = solutionEnum.Current;

                solutionStack.Push(solutionEnum);
                if (solution.state.dynamicContext.isPartialyResolved)
                {
                    solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
                    yield return solution;
                }
                else if (solution.state.dynamicContext.GetCurrentStatement() == null)
                {
                    yield return solution;
                }
                else { solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator()); }

            }
        }
    }
}