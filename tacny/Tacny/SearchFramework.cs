using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Tacny {


  [ContractClass(typeof(BaseSearchContract))]
  public interface ISearch {
    IEnumerable<ProofState> Search(ProofState state);
  }

  public enum Strategy {
    Undefined = 0,
    Bfs,
    Dfs
  }

  [ContractClassFor(typeof(ISearch))]
  // Validate the input before execution
  public abstract class BaseSearchContract : ISearch {
    public IEnumerable<ProofState> Search(ProofState state) {
      Contract.Requires(state != null);
      return default(IEnumerable<ProofState>);
    }
  }

  public class BaseSearchStrategy : ISearch {
    protected Strategy ActiveStrategy;
    protected static bool Verify;

    public BaseSearchStrategy(Strategy strategy, bool verify) {
      Verify = verify;
      ActiveStrategy = strategy;

    }

    protected BaseSearchStrategy() {

    }

    public IEnumerable<ProofState> Search(ProofState state) {
      Contract.Requires<ArgumentNullException>(state != null, "rootState");

      IEnumerable<ProofState> enumerable;
      switch (ActiveStrategy) {
        case Strategy.Bfs:
          enumerable = BreadthFirstSeach.Search(state);
          break;
        case Strategy.Dfs:
          enumerable = DepthFirstSeach.Search(state);
          break;
        case Strategy.Undefined:
          throw new tcce.UnreachableException();
        default:
          enumerable = BreadthFirstSeach.Search(state);
          break;
      }
      return enumerable;
    }


    internal static bool VerifyState(ProofState state) {
      return true;

    }
  }

  internal class BreadthFirstSeach : BaseSearchStrategy {

    internal new static IEnumerable<ProofState> Search(ProofState rootState) {

      var queue = new Queue<IEnumerator<ProofState>>();
      queue.Enqueue(Interpreter.EvalStep(rootState).GetEnumerator());
      while (queue.Count > 0) {
        // remove first enumerator on the queue
        var enumerator = queue.Dequeue();
        // if all the statements have been resolve skip
        if (!enumerator.MoveNext())
          continue;

        var proofState = enumerator.Current;
        queue.Enqueue(enumerator);

        if (Verify) {
          if (proofState.IsEvaluated() || proofState.IsPartiallyEvaluated()) {
            if (VerifyState(proofState)) {
              yield return proofState;
              yield break;
            }
            /*
             * verification failed, continue to the next state
             */
            if (proofState.IsEvaluated()) {
              continue;
            }
            /*
             * verification failed, but the evaluation is partial return
             * but continue evaluating the proofState
             */
            if (proofState.IsPartiallyEvaluated()) {
              yield return proofState;
              queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
            }
          }
          queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
        } else {
          if (!(proofState.IsEvaluated() || proofState.IsPartiallyEvaluated()))
            queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
          else {
            yield return proofState;
            if (proofState.IsPartiallyEvaluated()) {
              queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
            }
          }
        }
      }
    }
  }

  internal class DepthFirstSeach : BaseSearchStrategy {

    public new static IEnumerable<ProofState> Search(ProofState state) {
      return null;
      /*
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
      if (solution.IsEvaluated()) {
        if (verify) {
          if (VerifyState(solution)) {
            yield return solution;
            yield break;
          }
        }
        else {
          yield return solution;
        }
      }
      else if (solution.State.DynamicContext.isPartialyResolved) {
        if (verify) {
          if (VerifyState(solution)) {
            yield return solution;
            yield break;
          }
          solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
        }
        else {
          solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
          yield return solution;
        }
      }
      else {
        solutionStack.Push(Atomic.ResolveStatement(solution).GetEnumerator());
      }



    }
    */
    }
  }
}


