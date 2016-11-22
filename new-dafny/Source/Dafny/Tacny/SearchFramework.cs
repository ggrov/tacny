using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    Unresolved, // failed to resolve
    Verified,
    Failed, // resoved but cannot be proved
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




    public static VerifyResult VerifyState(ProofState state, ErrorReporterDelegate er) {
      // at the momemnt,we don't propagate errors from buanches to user, no need to use er, in the future this will
      // come to play when repair kicks in

      var prog =  Util.GenerateResolvedProg(state);
      if (prog == null)
        return VerifyResult.Unresolved;

   //   ErrorReporterDelegate tmp_er =
     //   errorInfo => { er?.Invoke(new CompoundErrorInformation(errorInfo.Tok, errorInfo.Msg, errorInfo, state)); };
      var result = Util.VerifyResolvedProg(prog, null);
/*
      ErrorReporterDelegate tmp_er =
        errorInfo => { er?.Invoke(new CompoundErrorInformation(errorInfo.Tok, errorInfo.Msg, errorInfo, state)); };
      var boogieProg = Util.Translate(prog, prog.Name, tmp_er);
      PipelineStatistics stats;
      List<ErrorInformation> errorList;

      //Console.WriteLine("call verifier in Tacny !!!");
      PipelineOutcome tmp = Util.BoogiePipeline(boogieProg,
        new List<string> { prog.Name }, prog.Name, tmp_er,
        out stats, out errorList, prog);
*/

      if(result)
        return VerifyResult.Verified;
      else {
        return VerifyResult.Failed;
      }
    }
  /*
  public static VerifyResult VerifyState0(ProofState state, ErrorReporterDelegate er) {
      //TODO: remove body list, only nedd one
      var bodyList = new Dictionary<ProofState, BlockStmt>();
      bodyList.Add(state, Util.InsertCode(state,
        new Dictionary<UpdateStmt, List<Statement>>(){
          {state.TacticApplication, state.GetGeneratedCode()}
        }));
    
        var memberList = Util.GenerateMembers(state, bodyList);
        var prog = Util.GenerateDafnyProgram(state, memberList.Values.ToList());

        Console.WriteLine("*********************Verifying Tacny Generated Lines *****************");
        var printer = new Printer(Console.Out);
      //  printer.PrintProgram(prog, false);
        foreach (var stmt in state.GetGeneratedCode()){
          printer.PrintStatement(stmt,0);
        }
      var result = Util.ResolveAndVerify(prog, errorInfo => { er?.Invoke(new CompoundErrorInformation(errorInfo.Tok, errorInfo.Msg, errorInfo, state)); });

      Console.WriteLine("\n*********************END*****************");

      if (result)
          return VerifyResult.Verified;
        else {
          //TODO: find which proof state verified (if any)
          //TODO: update verification results
          
          //  er();
          return VerifyResult.Failed;
        }
      }
      */
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
            case VerifyResult.Verified:
              proofState.MarkCurFrameAsTerminated(true);
              if (proofState.IsTerminated()){
                 yield return proofState;
                 yield break;
              }
              //queue.Enqueue(Interpreter.EvalStep(proofState).GetEnumerator());
              break;
            case VerifyResult.Failed:
              if (proofState.IsEvaluated()){
                proofState.MarkCurFrameAsTerminated(false);
                if(proofState.IsTerminated()) {
                  yield return proofState;
                  yield break;
                }
              }
              break;
            case VerifyResult.Unresolved:
              //discharge current branch if fails to resolve
              continue;
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
        //check if any new added coded reuqires to call verifier, or reach the last line of code
        if(proofState.IfVerify || proofState.IsEvaluated()) {
          proofState.IfVerify = false;
          switch(VerifyState(proofState, er)) {
            case VerifyResult.Verified:
              proofState.MarkCurFrameAsTerminated(true);
              if(proofState.IsTerminated()) {
                yield return proofState;
                yield break;
             }
              //stack.Push(enumerator);
              //enumerator = (Interpreter.EvalStep(proofState).GetEnumerator());
              break;
            case VerifyResult.Failed:
              if(proofState.IsEvaluated()) {
                proofState.MarkCurFrameAsTerminated(false);
                if(proofState.IsTerminated()) {
                  yield return proofState;
                  yield break;
                }
              }
              break;
            case VerifyResult.Unresolved:
              //discharge current branch if fails to resolve
              continue;
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
          //push the current one to the stack
          stack.Push(enumerator);
          //move to the next stmt
          enumerator = (Interpreter.EvalStep(proofState).GetEnumerator());
        }
      }
    }
  }
}


