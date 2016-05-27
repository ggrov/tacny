using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {

  [ContractClass(typeof(SearchContract))]
  public interface ISearch {
    IEnumerable<Solution> Search(Atomic atomic, bool verify = true);
    //  IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic ac);
  }
  public enum Strategy {
    Bfs = 0,
    Dfs
  }
  [ContractClassFor(typeof(ISearch))]
  // Validate the input before execution
  public abstract class SearchContract : ISearch {
    public IEnumerable<Solution> Search(Atomic atomic, bool verify = true) {
      Contract.Requires(atomic != null);

      yield break;
    }
  }

  public class SearchStrategy : ISearch {
    private Strategy _activeStrategy = Strategy.Bfs;
    public SearchStrategy(Strategy strategy) {
      if (TacnyOptions.O.EnableSearch >= 0) {
        try {
          _activeStrategy = (Strategy)TacnyOptions.O.EnableSearch;
        } catch {
          _activeStrategy = Strategy.Bfs;
        }
      } else
        _activeStrategy = strategy;

    }

    protected SearchStrategy() {

    }

    public IEnumerable<Solution> Search(Atomic atomic, bool verify = true) {
      IEnumerable<Solution> enumerable;
      switch (_activeStrategy) {
        case Strategy.Bfs:
          enumerable = BreadthFirstSeach.Search(atomic, verify);
          break;
        case Strategy.Dfs:
          enumerable = DepthFirstSeach.Search(atomic, verify);
          break;
        default:
          enumerable = BreadthFirstSeach.Search(atomic, verify);
          break;
      }
      // return a fresh copy of the atomic
      foreach (var item in enumerable) {
        yield return new Solution(item.State.Copy());
      }
    }

    public static Strategy GetSearchStrategy(ITactic tac) {
      Contract.Requires<ArgumentNullException>(tac != null);
      MemberDecl md = tac as MemberDecl;
      Attributes attrs = md?.Attributes;


      if (attrs?.Name != "search")
        return Strategy.Bfs;

      Expression expr = attrs.Args.FirstOrDefault();
      var name = (expr as NameSegment)?.Name;
      switch (name?.ToUpper()) {
        case "BFS":
          return Strategy.Bfs;
        case "DFS":
          return Strategy.Dfs;
        default:
          Contract.Assert(false, (Error.MkErr(expr, 19, name)));
          return Strategy.Bfs;
      }
    }


    internal static bool VerifySolution(Solution solution) {
      if (!solution.State.StaticContext.program.HasError()) {
        // return the valid solution and terminate
        return true;
      } // if verifies break else continue to the next solution
      if (solution.State.ResolveAndVerify(solution)) {
        return !solution.State.StaticContext.program.HasError();
      }
      return false;
    }
  }

  internal class BreadthFirstSeach : SearchStrategy {
    public new static IEnumerable<Solution> Search(Atomic atomic, bool verify = true) {
      Debug.WriteLine($"Resolving tactic {atomic.DynamicContext.tactic}");

      //local solution list
      var result = atomic == null ? new List<Solution>() : new List<Solution> { new Solution(atomic) };
      while (true) {
        var interm = new List<Solution>();
        if (result.Count == 0) {
          yield break;
        }
        // iterate every solution
        foreach (var item in result) {
          // lazily resolve a statement in the solution
          foreach (var solution in Atomic.ResolveStatement(item)) {
            // validate result
            if (solution.IsResolved())
            {
              if (verify) {
                if (!VerifySolution(solution)) continue;
                yield return solution; yield break;
              }
              yield return solution;
            }
            else if (solution.State.DynamicContext.isPartialyResolved) {
              if (verify)
              {
                if (VerifySolution(solution)) { yield return solution; yield break; }
                interm.Add(solution);
              }
              else { interm.Add(solution); yield return solution; }
            } else {
              interm.Add(solution);
            }
          }
        }
        result.Clear();
        result.AddRange(interm);
      }
    }

    public static IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic) {
      Atomic ac = atomic.Copy();
      ac.DynamicContext.tacticBody = body.Body;
      ac.DynamicContext.ResetCounter();
      List<Solution> result = new List<Solution> { new Solution(ac) };
      // search strategy for body goes here
      while (true) {
        List<Solution> interm = new List<Solution>();
        if (result.Count == 0)
          break;
        foreach (var solution in result) {
          foreach (var item in Atomic.ResolveStatement(solution)) {
            if (item.State.DynamicContext.isPartialyResolved) {
              { interm.Add(item); }
              yield return item;
            } else if (item.State.DynamicContext.GetCurrentStatement() == null) { yield return item; } else { interm.Add(item); }
          }
        }

        result.Clear();
        result.AddRange(interm);
      }
    }
  }

  internal class DepthFirstSeach : SearchStrategy {
    public new static IEnumerable<Solution> Search(Atomic atomic, bool verify = true) {

      Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();

      if (atomic != null)
        solutionStack.Push(Atomic.ResolveStatement(new Solution(atomic)).GetEnumerator());

      while (true) {
        if (solutionStack.Count == 0) {

          yield break;
        }

        var solutionEnum = solutionStack.Pop();

        // if the solution is fully resolved skip resolution
        if (!solutionEnum.MoveNext())
          continue;

        var solution = solutionEnum.Current;
        solutionStack.Push(solutionEnum);
        if (solution.IsResolved()) {
          if (verify) {
            if (VerifySolution(solution)) { yield return solution; yield break; }
          } else {
            yield return solution;
          }
        } else if (solution.State.DynamicContext.isPartialyResolved) {
          if (verify)
          {
            if (VerifySolution(solution)) { yield return solution; yield break; }
            solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
          }
          else {
            solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
            yield return solution;
          }
        } else {
          solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
        }



      }

    }

    public static IEnumerable<Solution> SearchBlockStmt(BlockStmt body, Atomic atomic) {
      Atomic ac = atomic.Copy();
      ac.DynamicContext.tacticBody = body.Body;
      ac.DynamicContext.ResetCounter();
      Stack<IEnumerator<Solution>> solutionStack = new Stack<IEnumerator<Solution>>();
      solutionStack.Push(Atomic.ResolveStatement(new Solution(ac)).GetEnumerator());

      while (true) {
        if (solutionStack.Count == 0) {

          yield break;
        }

        var solutionEnum = solutionStack.Pop();

        // if the solution is fully resolved skip resolution
        if (!solutionEnum.MoveNext())
          continue;
        var solution = solutionEnum.Current;

        solutionStack.Push(solutionEnum);
        if (solution.State.DynamicContext.isPartialyResolved) {
          solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
          yield return solution;
        } else if (solution.State.DynamicContext.GetCurrentStatement() == null) {
          yield return solution;
        } else { solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator()); }

      }
    }
  }
}
