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
          enumerable = DepthFirstSeach.Search(state, er);
          break;
        case Strategy.Undefined:
          throw new tcce.UnreachableException();
        default:
          enumerable = DepthFirstSeach.Search(state, er);
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
      /*      if (_proofList == null)
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
       */
      var bodyList = new Dictionary<ProofState, BlockStmt>();
      bodyList.Add(state, Util.InsertCode(state,
        new Dictionary<UpdateStmt, List<Statement>>(){
          {state.TacticApplication, state.GetGeneratedCode()}
        }));
    
            var memberList = Util.GenerateMembers(state, bodyList);
        var prog = Util.GenerateDafnyProgram(state, memberList.Values.ToList());

        var printer = new Printer(Console.Out);
        printer.PrintProgram(prog, false);

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
        //check if any new added code reuqires to call the dafny to verity, or reach the last line of code
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
              // in a controal block which can be returned
              if (proofState.IsCurrentCntlTermianted()){
                 yield return proofState;
                 yield break;
              }
             // more stmt block need to be evaluaed in the queue, pop the current frame and continue to evalaute
              proofState.RemoveFrame();
              queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
              break;
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
  
    internal new static IEnumerable<ProofState> Search(ProofState rootState, ErrorReporterDelegate er){
      var stack = new Stack<IEnumerator<ProofState>>();
      stack.Push(Interpreter.EvalStep(rootState).GetEnumerator());


      IEnumerator<ProofState> enumerator = Enumerable.Empty<ProofState>().GetEnumerator();

      while(stack.Count > 0) {
        if(!enumerator.MoveNext()) {
          enumerator = stack.Pop();
          if(!enumerator.MoveNext())
            continue;
        }
        var proofState = enumerator.Current;
        //check if any new added coded reuqires to call the dafny to verity, or reach the last line of code
        if(proofState.IfVerify || proofState.IsEvaluated()) {
          proofState.IfVerify = false;
          switch(VerifyState(proofState, er)) {
            case VerifyResult.Verified:
              // in a controal block which can be returned
              if(proofState.IsCurrentCntlTermianted()) {
                yield return proofState;
                yield break;
              }
              // more stmt block need to be evalauted in the stack, pop the current frame and continue to evalaute
              proofState.RemoveFrame();
              stack.Push(enumerator);
              enumerator = (Interpreter.EvalStep(proofState).GetEnumerator());
              break;
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
          stack.Push(enumerator);
          enumerator = (Interpreter.EvalStep(proofState).GetEnumerator());
        }
      }
    }
  }
}


