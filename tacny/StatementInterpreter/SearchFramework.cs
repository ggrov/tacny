using System;
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
        IEnumerable<Solution> Search(List<Solution> input, Atomic atomic = null, bool verify = true);
        IEnumerable<Solution> SearchBlockStmt(Microsoft.Dafny.BlockStmt body, Atomic ac);
    }

    [ContractClassFor(typeof(ISearch))]
    // Validate the input before execution
    public abstract class ISearchContract : ISearch
    {
        public IEnumerable<Solution> Search(List<Solution> input, Atomic atomic = null, bool verify = true)
        {
            Contract.Requires(input != null);

            yield break;
        }

        public IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic ac)
        {
            Contract.Requires(body != null);
            Contract.Requires(ac != null);

            yield break;
        }
    }

    public class SearchStrategy : ISearch
    {
        public enum Strategy  {
            BFS,
            DFS
        }

        private Strategy ActiveStrategy = Strategy.BFS;
        public SearchStrategy(Strategy strategy)
        {
            ActiveStrategy = strategy;
        }

        protected SearchStrategy()
        {

        }

        public IEnumerable<Solution> Search(List<Solution> input, Atomic atomic = null, bool verify = true)
        {
            IEnumerable<Solution> enumerable;
            switch(ActiveStrategy)
            {
                case Strategy.BFS:
                    enumerable = BreadthFirstSeach.Search(input, atomic, verify);
                    break;
                case Strategy.DFS:
                    enumerable = DepthFirstSeach.Search(input, atomic, verify);
                    break;
                default:
                    enumerable = BreadthFirstSeach.Search(input, atomic, verify);
                    break;
            }
            // return a fresh copy of the atomic
            foreach (var item in enumerable)
                yield return new Solution(item.state.Copy());
            yield break;
        }

        public IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic)
        {
            IEnumerable<Solution> enumerable;
            switch (ActiveStrategy)
            {
                case Strategy.BFS:
                    enumerable = BreadthFirstSeach.SearchBlockStmt(body, atomic);
                    break;
                case Strategy.DFS:
                    enumerable = DepthFirstSeach.SearchBlockStmt(body, atomic);
                    break;
                default:
                    enumerable = BreadthFirstSeach.SearchBlockStmt(body, atomic);
                    break;
            }

            foreach (var item in enumerable)
            {
                // fix context
                    yield return new Solution(item.state.Copy());
            }
            yield break;
        }

        internal static Strategy GetSearchStrategy(Tactic tac)
        {
            Contract.Requires<ArgumentNullException>(tac != null);
            Attributes attrs = tac.Attributes;
            if(attrs != null)
            {
                if(attrs.Name == "search")
                {
                    Expression expr = attrs.Args.FirstOrDefault();
                    if(expr != null)
                    {
                        // the search strategy is expected to be a name segment
                        var ns = expr as NameSegment;
                        if(ns != null)
                        {
                            switch(ns.Name.ToUpper())
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

            return Strategy.BFS;
        }
    }

    public class BreadthFirstSeach : SearchStrategy
    {
        public static new IEnumerable<Solution> Search(List<Solution> input, Atomic atomic = null, bool verify = true)
        {
            if (atomic != null)
                Debug.WriteLine(String.Format("Resolving tactic {0}", atomic.localContext.tactic));
            Debug.Indent();
            //local solution list
            List<Solution> result = atomic == null ? new List<Solution>() : new List<Solution>() { new Solution(atomic) };
            // if previous solutions exist, add them 
            if (input.Count > 0)
                result.AddRange(input);

            while (true)
            {
                List<Solution> temp = new List<Solution>();
                if (result.Count == 0)
                {
                    Debug.Unindent();
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
                                if (!solution.state.globalContext.program.HasError())
                                {
                                    // return the valid solution and terminate
                                    yield return solution;
                                    yield break;
                                }
                                else
                                {  // if verifies break else continue
                                    solution.state.ResolveAndVerify(solution);
                                    if (!solution.state.globalContext.program.HasError())
                                    {
                                        // return the valid solution and terminate
                                        yield return solution;
                                        yield break;
                                    }
                                    else { continue; }
                                }
                            }
                            else
                            {
                                yield return solution;
                            }

                        }
                        else
                        {
                            temp.Add(solution);
                        }
                    }
                }
                result.Clear();
                result.AddRange(temp);
            }
        }

        public static new IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic)
        {
            Atomic ac = atomic.Copy();
            ac.localContext.tacticBody = body.Body;
            ac.localContext.ResetCounter();
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
                        if (item.state.localContext.isPartialyResolved)
                        {
                            { interm.Add(item); }
                            yield return item;
                        }
                        else if (item.state.localContext.GetCurrentStatement() == null) { yield return item; }
                        else { interm.Add(item); }
                    }
                }

                result.Clear();
                result.AddRange(interm);
            }
        }
    }

    public class DepthFirstSeach : SearchStrategy
    {
        public static new IEnumerable<Solution> Search(List<Solution> input, Atomic atomic = null, bool verify = true)
        {
            if (atomic != null)
                Debug.WriteLine(String.Format("Resolving tactic {0}", atomic.localContext.tactic));
            Debug.Indent();
            Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();
            //local solution list
            input.Reverse();
            foreach (var item in input)
                solutionStack.Push(Atomic.ResolveStatement(item).GetEnumerator());

            if (atomic != null)
                solutionStack.Push(Atomic.ResolveStatement(new Solution(atomic)).GetEnumerator());

            while (true)
            {
                if (solutionStack.Count == 0)
                {
                    Debug.Unindent();
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
                        if (!solution.state.globalContext.program.HasError())
                        {
                            yield return solution;
                            yield break;
                        }
                        else
                        {  // if verifies break else continue
                            solution.state.ResolveAndVerify(solution);
                            if (!solution.state.globalContext.program.HasError())
                            {
                                yield return solution;
                                // return the valid solution and terminate
                                yield break;
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    else
                    {
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
            ac.localContext.tacticBody = body.Body;
            ac.localContext.ResetCounter();
            Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();
            solutionStack.Push(Atomic.ResolveStatement(new Solution(ac)).GetEnumerator());

            while(true)
            {
                if (solutionStack.Count == 0)
                {
                    Debug.Unindent();
                    yield break;
                }

                var solutionEnum = solutionStack.Pop();

                // if the solution is fully resolved skip resolution
                if (!solutionEnum.MoveNext())
                    continue;
                var solution = solutionEnum.Current;

                solutionStack.Push(solutionEnum);
                if (solution.state.localContext.isPartialyResolved)
                {
                    solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
                    yield return solution;
                }
                else if (solution.state.localContext.GetCurrentStatement() == null)
                {
                    yield return solution;
                }
                else { solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator()); }

            }
        }
    }
}
