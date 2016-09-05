using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;

namespace Tacny {


  [ContractClass(typeof(BaseSearchContract))]
  public interface ISearch {
    IEnumerable<ProofState> Search(ProofState state, ErrorReporterDelegate er);
  }


  public enum Strategy {
    Undefined = 0,
    Bfs,
    Dfs
  }

  public enum VerifyResult {
    Cached, // solution is cached for multi solution resolution
    Verified,
    Failed,
    Partial, //TODO: partial when tactic and dafny succeed, but boogie fails
  }

  [ContractClassFor(typeof(ISearch))]
  // Validate the input before execution
  public abstract class BaseSearchContract : ISearch {
    public IEnumerable<ProofState> Search(ProofState state, ErrorReporterDelegate er) {
      Contract.Requires(state != null);
      return default(IEnumerable<ProofState>);
    }
  }

  public class BaseSearchStrategy : ISearch {
    protected Strategy ActiveStrategy;
    protected static bool Verify;
    protected const int SolutionCounter = 1;
    public BaseSearchStrategy(Strategy strategy, bool verify) {
      Verify = verify;
      ActiveStrategy = strategy;

    }

    protected BaseSearchStrategy() {

    }
    
    public IEnumerable<ProofState> Search(ProofState state, ErrorReporterDelegate er) {
      Contract.Requires<ArgumentNullException>(state != null, "rootState");
	  
	  IEnumerable<ProofState> enumerable;      
      switch (ActiveStrategy) {
        case Strategy.Bfs:
          enumerable = BreadthFirstSeach.Search(state, er);
          break;
        case Strategy.Dfs:
          enumerable = DepthFirstSeach.Search(state);
          break;
        case Strategy.Undefined:
          throw new tcce.UnreachableException();
        default:
          enumerable = BreadthFirstSeach.Search(state, er);
          break;
      }
      return enumerable;
    }

    public static void ResetProofList()
    {
        _proofList = null;
    }

    private static List<ProofState> _proofList;
    internal static VerifyResult VerifyState(ProofState state, ErrorReporterDelegate er) {
      if (_proofList == null)
        _proofList = new List<ProofState>();
      if (_proofList.Count + 1 < SolutionCounter) {
        _proofList.Add(state);
        return VerifyResult.Cached;
      } else {
        _proofList.Add(state);
        var bodyList = new Dictionary<ProofState, BlockStmt>();
        foreach (var proofState in _proofList) {
          bodyList.Add(proofState, Util.InsertCode(proofState,
            new Dictionary<UpdateStmt, List<Statement>>() {
              {proofState.TacticApplication, proofState.GetGeneratedCode()}
            }));
        }
        var memberList = Util.GenerateMembers(state, bodyList);
        var prog = Util.GenerateDafnyProgram(state, memberList.Values.ToList());
        var result = Util.ResolveAndVerify(prog, errorInfo => { er?.Invoke(new CompoundErrorInformation(errorInfo.Tok, errorInfo.Msg, errorInfo, state)); });
        if (result.Count == 0)
          return VerifyResult.Verified;
        else {
          //TODO: find which proof state verified (if any)
          //TODO: update verification results
          
          //  er();
          return VerifyResult.Failed;
        }
      }
    }
  }

  internal class BreadthFirstSeach : BaseSearchStrategy {

    internal new static IEnumerable<ProofState> Search(ProofState rootState, ErrorReporterDelegate er){

      var queue = new Queue<IEnumerator<ProofState>>();
      queue.Enqueue(Interpreter.EvalStep(rootState).GetEnumerator());


      IEnumerator<ProofState> enumerator = Enumerable.Empty<ProofState>().GetEnumerator();

      while (queue.Count > 0){
        // check if there is any more item in the enumerartor, if so, MoveNext will move to the next item
        if (!enumerator.MoveNext()){
          // if no item in the current enumerator, pop a new enumerator from the queie, 
          enumerator = queue.Dequeue();
          // set the start point for enumulator, if there is no valid start point, i.e. empty, skip this one
          if (!enumerator.MoveNext())
            continue;
        }
        var proofState = enumerator.Current;
        //check if any new dded coded reuqires call the dafny to verity, or reach the last line of code
        if (proofState.IfVerify || proofState.IsEvaluated()) {
          proofState.IfVerify = false;
          switch (VerifyState(proofState, er)){
            case VerifyResult.Cached:
              //TODO: is this code active ?
              Console.WriteLine("Verify Cached is indeed in use ?!");
              if (proofState.IsPartiallyEvaluated()){
                yield return proofState;
                queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
              }
              continue;
            case VerifyResult.Verified:
              yield return proofState;
              yield break;
            case VerifyResult.Failed:
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        }
        /*
       * when failed, check if this mmethod is evaluated , i.e. all tstmt are evalauted,
       * if so, dischard this branch and continue with the next one
       * otherwise, continue to evaluate the next stmt
       */
        if(!proofState.IsEvaluated()) {
          queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
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


