using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Microsoft.Boogie;
using Microsoft.Dafny;
//using LiteralExpr = Microsoft.Dafny.LiteralExpr;
using Dafny = Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;
using Type = Microsoft.Dafny.Type;

namespace Tacny {
  public class Interpreter {
    private static Interpreter _i;
    private Stack<Dictionary<IVariable, Type>> _frame;

    private readonly ProofState _state;
    private readonly ErrorReporter _errorReporter;

    private static ErrorReporterDelegate _errorReporterDelegate;

    private readonly Dictionary<UpdateStmt, List<Statement>> _resultList;
    private Interpreter(Program program, Program unresolvedProgram = null) {
      Contract.Requires(tcce.NonNull(program));
      // initialize state
      _errorReporter = new ConsoleErrorReporter();
      _state = new ProofState(program, _errorReporter, unresolvedProgram);
      _frame = new Stack<Dictionary<IVariable, Type>>();
      _resultList = new Dictionary<UpdateStmt, List<Statement>>();
    }


    [ContractInvariantMethod]
    private void ObjectInvariant() {
      Contract.Invariant(tcce.NonNull(_state));
      Contract.Invariant(tcce.NonNull(_frame));
      Contract.Invariant(_errorReporter != null);
    }

    public static MemberDecl FindAndApplyTactic(Program program, MemberDecl target, ErrorReporterDelegate erd, Program unresolvedProgram = null) {
      Contract.Requires(program != null);
      Contract.Requires(target != null);
      _i = new Interpreter(program, unresolvedProgram);
      _errorReporterDelegate = erd;
      var result = _i.FindTacticApplication(target);

      var p = new Printer(Console.Out);
      p.PrintMembers(new List<MemberDecl>() { result }, 0, "");

      _errorReporterDelegate = null;
      return result;
    }

    public static List<Statement> FindSingleTactic(Program program, MemberDecl target,
      UpdateStmt chosenTacticCall, ErrorReporterDelegate erd, Program unresolvedProgram) {
      Contract.Requires(program != null);
      Contract.Requires(target != null);
      var i = new Interpreter(program, unresolvedProgram);
      _errorReporterDelegate = erd;
      var list = i.FindSingleTacticApplication(target, chosenTacticCall);
      _errorReporterDelegate = null;
      return list;
    }

    private List<Statement> FindSingleTacticApplication(MemberDecl target, UpdateStmt chosenTacticCall) {
      Contract.Requires(tcce.NonNull(target));
      _frame = new Stack<Dictionary<IVariable, Type>>();
      var method = target as Method;
      if (method == null) return null;
      _state.SetTopLevelClass(method.EnclosingClass?.Name);
      _state.TargetMethod = target;
      var dict = method.Ins.Concat(method.Outs)
        .ToDictionary<IVariable, IVariable, Type>(item => item, item => item.Type);
      _frame.Push(dict);
      SearchBlockStmt(method.Body);
      _frame.Pop();
      Contract.Assert(_frame.Count == 0);
      return (from r in _resultList
              where r.Key.Tok.pos == chosenTacticCall.Tok.pos
              select r.Value).FirstOrDefault();
    }


    private MemberDecl FindTacticApplication(MemberDecl target) {
      Contract.Requires(tcce.NonNull(target));
      // initialize new stack for variables
      _frame = new Stack<Dictionary<IVariable, Type>>();
      // clean up the result list
      _resultList.Clear();
      var method = target as Method;
      if (method != null) {
        _state.SetTopLevelClass(method.EnclosingClass?.Name);
        _state.TargetMethod = target;
        var dict = method.Ins.Concat(method.Outs)
          .ToDictionary<IVariable, IVariable, Type>(item => item, item => item.Type);
        _frame.Push(dict);
        SearchBlockStmt(method.Body);
        dict = _frame.Pop();
        // sanity check
        Contract.Assert(_frame.Count == 0);


        _state.ResultCache.Add(new ProofState.TacticCache(method?.Name, _resultList.Copy()));

    //    var method2 = Util.GenDeclFromTacCode(_state, _resultList) as Method;
        
        var body = Util.InsertCode(_state, _resultList);
        method.Body.Body.Clear();
        if (body != null)
          method.Body.Body.AddRange(body.Body);
        
      }
      return method;
    }

    // Find tactic application and resolve it
    private void SearchBlockStmt(BlockStmt body) {
      Contract.Requires(tcce.NonNull(body));
            
      BaseSearchStrategy.ResetProofList();
      _frame.Push(new Dictionary<IVariable, Type>());
      foreach (var stmt in body.Body) {
        if (stmt is VarDeclStmt) {
          var vds = stmt as VarDeclStmt;
          // register local variable declarations
          foreach (var local in vds.Locals) {
            try {
              _frame.Peek().Add(local, local.Type);
            } catch (Exception e) {
              //TODO: some error handling when target is not resolved
              Console.Out.WriteLine(e.Message);
            }
          }
        } else if (stmt is IfStmt) {
          var ifStmt = stmt as IfStmt;
          SearchIfStmt(ifStmt);

        } else if (stmt is WhileStmt) {
          var whileStmt = stmt as WhileStmt;
          SearchBlockStmt(whileStmt.Body);
        } else if (stmt is UpdateStmt) {
          var us = stmt as UpdateStmt;
          if (_state.IsTacticCall(us)) {
            var list = StackToDict(_frame);
            var result = ApplyTactic(_state, list, us);
            if (result != null)
             _resultList.Add(us.Copy(), result.GetGeneratedCode().Copy());
          }
        } else if (stmt is BlockStmt) {
          //TODO:
        }
      }
      _frame.Pop();
    }

    private void SearchIfStmt(IfStmt ifStmt) {
      Contract.Requires(tcce.NonNull(ifStmt));
      SearchBlockStmt(ifStmt.Thn);
      if (ifStmt.Els == null) return;
      var els = ifStmt.Els as BlockStmt;
      if (els != null) {
        SearchBlockStmt(els);
      } else if (ifStmt.Els is IfStmt) {
        SearchIfStmt((IfStmt)ifStmt.Els);
      }
    }

    private static Dictionary<IVariable, Type> StackToDict(Stack<Dictionary<IVariable, Type>> stack) {
      Contract.Requires(stack != null);
      Contract.Ensures(Contract.Result<Dictionary<IVariable, Type>>() != null);
      var result = new Dictionary<IVariable, Type>();
      foreach (var dict in stack) {
        dict.ToList().ForEach(x => result.Add(x.Key, x.Value));
      }
      return result;
    }

    public static bool ParsePartialAttribute(Attributes attr) {
      Contract.Requires(attr != null);
      if (attr.Name == "partial")
        return true;

      return attr.Prev != null && ParsePartialAttribute(attr.Prev);
    }

    public static bool IsPartial(ProofState state, UpdateStmt tacticApplication) {
      //still need to check the localtion of the application, is it the last call ? is it a neswted call ?
      if (state.TacticInfo.IsPartial) {
        return true;
      }

      return tacticApplication.Rhss[0].Attributes != null && ParsePartialAttribute(tacticApplication.Rhss[0].Attributes);
    }

    public static ProofState ApplyTactic(ProofState state, Dictionary<IVariable, Type> variables,
      UpdateStmt tacticApplication) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(variables));
      Contract.Requires<ArgumentNullException>(tcce.NonNull(tacticApplication));
      Contract.Requires<ArgumentNullException>(state != null, "state");
      state.InitState(tacticApplication, variables);

      var search = new BaseSearchStrategy(state.TacticInfo.SearchStrategy, !IsPartial(state, tacticApplication));
      return search.Search(state, _errorReporterDelegate).FirstOrDefault();
    }

    public static IEnumerable<ProofState> ApplyNestedTactic(ProofState state, Dictionary<IVariable, Type> variables,
      UpdateStmt tacticApplication) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(variables));
      Contract.Requires<ArgumentNullException>(tcce.NonNull(tacticApplication));
      Contract.Requires<ArgumentNullException>(state != null, "state");
      state.InitState(tacticApplication, variables);
      var search = new BaseSearchStrategy(state.TacticInfo.SearchStrategy, false);
      foreach (var result in search.Search(state, _errorReporterDelegate)) {
        var c = state.Copy();
        c.AddStatementRange(result.GetGeneratedCode());
        yield return c;
      }
    }

    public static IEnumerable<ProofState> EvaluateBlockStmt(ProofState state, BlockStmt blockStmt) {
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentNullException>(blockStmt != null, "stmt");
      state.AddNewFrame(blockStmt);
      var search = new BaseSearchStrategy(state.TacticInfo.SearchStrategy, false);
      foreach (var result in search.Search(state, _errorReporterDelegate)) {
        var c = state.Copy();
        c.AddStatementRange(result.GetGeneratedCode());
        yield return c;
      }
      if (!state.RemoveFrame()) {
        throw new InvalidOperationException("tried to pop more frames than were pushed");
      }
    }

    public static void PrepareFrame(BlockStmt body, ProofState state) {
      Contract.Requires<ArgumentNullException>(body != null, "body");
      Contract.Requires<ArgumentNullException>(state != null, "state");
      state.AddNewFrame(body);
      // call the search engine
      var search = new BaseSearchStrategy(state.TacticInfo.SearchStrategy, true);
      search.Search(state, _errorReporterDelegate);
      state.RemoveFrame();
    }

    public static IEnumerable<ProofState> EvalStep(ProofState state) {
      Contract.Requires<ArgumentNullException>(state != null, "state");

      IEnumerable<ProofState> enumerable = null;
      var stmt = state.GetStmt();
      if (stmt is TacticVarDeclStmt) {
        enumerable = RegisterVariable(stmt as TacticVarDeclStmt, state);
      } else if (stmt is UpdateStmt) {
        var us = stmt as UpdateStmt;
        if (state.IsLocalAssignment(us)) {
          enumerable = UpdateLocalValue(us, state);
        } else  if (state.IsArgumentApplication(us)){
          //TODO: argument application
         // not sure what this is for
        }
        else if (state.IsTacticCall(us)){
          enumerable = ApplyNestedTactic(state.Copy(), state.DafnyVars(), us);
        }
        else{// apply atomic
          string sig = Util.GetSignature(us);
          //Firstly, check if this is a projection function
          var types =
            Assembly.GetAssembly(typeof(Atomic.Atomic))
              .GetTypes()
              .Where(t => t.IsSubclassOf(typeof(Atomic.Atomic)));
          foreach(var fType in types) {
            var porjInst = Activator.CreateInstance(fType) as Atomic.Atomic;
            if(sig == porjInst?.Signature) {
              //TODO: validate input countx
              enumerable = porjInst?.Generate(us, state);
            }
          }
        }
      } else if (stmt is AssignSuchThatStmt) {
        enumerable = EvaluateSuchThatStmt((AssignSuchThatStmt)stmt, state);
      } else if (stmt is PredicateStmt) {
        enumerable = ResolvePredicateStmt((PredicateStmt)stmt, state);
      } else if(stmt is TacnyCasesBlockStmt) {
        //TODO: tmatch
        enumerable = new Tacny.Atomic.Match(stmt).Generate((TacnyCasesBlockStmt)stmt, state);
      } else if (stmt is TStatement) {
        //TODO: Evaluate tactic statement
      } else if (stmt is IfStmt || stmt is WhileStmt) {
        enumerable = ResolveFlowControlStmt(stmt, state);
      }else {
        enumerable = DefaultAction(stmt, state);
      }

      foreach (var item in enumerable)
        yield return item.Copy();
    }

    private static IEnumerable<ProofState> ResolveFlowControlStmt(Statement stmt, ProofState state) {
      Language.FlowControlStmt fcs = null;
      if (stmt is IfStmt) {
        fcs = new Language.IfStmt();
      } else if (stmt is WhileStmt) {
        //TODO: while statemenet
      } else {
        Contract.Assert(false);
        return null;
      }
      return fcs.Generate(stmt, state);
    }


    public static IEnumerable<ProofState> ResolvePredicateStmt(PredicateStmt predicate, ProofState state) {
      Contract.Requires<ArgumentNullException>(predicate != null, "predicate");
      foreach (var result in EvaluateTacnyExpression(state, predicate.Expr)) {
        var resultExpression = result is IVariable ? Util.VariableToExpression(result as IVariable) : result as Expression;
        PredicateStmt newPredicate;
        if (predicate is AssertStmt) {
          newPredicate = new AssertStmt(predicate.Tok, predicate.EndTok, resultExpression, predicate.Attributes);
        } else {
          newPredicate = new AssumeStmt(predicate.Tok, predicate.EndTok, resultExpression, predicate.Attributes);
        }
        var copy = state.Copy();
        copy.AddStatement(newPredicate);
        copy.IfVerify = true;
        yield return copy;
      }
    }
/*
    public static IEnumerable<object> GenerateFromApplySuffix(string sig, System.Type typ, ProofState state, ApplySuffix aps) {
      var types =
        Assembly.GetAssembly(typ).GetTypes().Where(t => t.IsSubclassOf(typ));
      foreach (var type in types){
        var resolverInstance = Activator.CreateInstance(type) as EAtomic.EAtomic;
        if (sig == resolverInstance?.Signature){
          //TODO: validate input countx, as well types of hte the args
          foreach (var item in resolverInstance?.Generate(aps, state))
            yield return item;
        }
      }
    }
*/
    public static IEnumerable<object> EvaluateTacnyExpression(ProofState state, Expression expr) {
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentNullException>(expr != null, "expr");
      if(expr is NameSegment) {
        var ns = (NameSegment)expr;
        if(state.HasLocalValue(ns.Name)) {
          yield return state.GetLocalValue(ns.Name);
        } else {
          yield return ns;
        }
      } else if(expr is ApplySuffix) {
        var aps = (ApplySuffix)expr;
        if(state.IsTacticCall(aps)) {
          var us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>() { aps.Lhs },
            new List<AssignmentRhs>() { new ExprRhs(aps) });
          foreach(var item in ApplyNestedTactic(state, state.DafnyVars(), us).Select(x => x.GetGeneratedCode())) {
            yield return item;
          }
        } else if (aps.Lhs is ExprDotName){
          foreach (var item in EvaluateTacnyExpression(state, aps.Lhs)){
            if (item is Expression){
              yield return new ApplySuffix(aps.tok, (Expression) item, aps.Args);
            }
            else{
              Contract.Assert(false, "Unexpected ExprNotName case");
            }
          }
        }
        else{
          // get the keyword of this application
          string sig = Util.GetSignature(aps);
          //Firstly, check if this is a projection function
          var types =
            Assembly.GetAssembly(typeof(EAtomic.EAtomic))
              .GetTypes()
              .Where(t => t.IsSubclassOf(typeof(EAtomic.EAtomic)));
          foreach (var fType in types){
            var porjInst = Activator.CreateInstance(fType) as EAtomic.EAtomic;
            if (sig == porjInst?.Signature){
              //TODO: validate input countx
              var enumerable = porjInst?.Generate(aps, state);
              if (enumerable != null)
                foreach (var item in enumerable)
                  yield return item;
            }
            //If not projection, then try to evaluate as tacny expression
            // using reflection find all classes that extend EAtomic
            types =
              Assembly.GetAssembly(typeof(EAtomic.EAtomic))
                .GetTypes()
                .Where(t => t.IsSubclassOf(typeof(EAtomic.EAtomic)));
            foreach (var eType in types){
              var eatomInst = Activator.CreateInstance(eType) as EAtomic.EAtomic;
              if (sig == eatomInst?.Signature){
                //TODO: validate input countx
                var enumerable = eatomInst?.Generate(aps, state);
                if (enumerable != null)
                  foreach (var item in enumerable)
                    yield return item;
              }
              // if we reached this point, rewrite  the apply suffix
              foreach (var item in EvaluateTacnyExpression(state, aps.Lhs)){
                if (!(item is NameSegment)){
                  //TODO: warning
                }
                else{
                  var argList = new List<Expression>();
                  foreach (var arg in aps.Args){
                    foreach (var result in EvaluateTacnyExpression(state, arg)){
                      if (result is Expression)
                        argList.Add(result as Expression);
                      else
                        argList.Add(Util.VariableToExpression(result as IVariable));
                      break;
                    }
                  }
                  yield return new ApplySuffix(aps.tok, aps.Lhs, argList);
                }
              }
            }
          }
        }
      } else if(expr is ExprDotName) {
        var edn = (ExprDotName)expr;
        var ns = edn.Lhs as NameSegment;
        if(ns != null && state.ContainsVariable(ns)) {
          var newLhs = state.GetLocalValue(ns);
          var lhs = newLhs as Expression;
          if(lhs != null)
            yield return new ExprDotName(edn.tok, lhs, edn.SuffixName, edn.OptTypeArguments);
        }
        yield return edn;
      } else if(expr is UnaryOpExpr) {
        var op = (UnaryOpExpr)expr;
        foreach(var result in EvaluateTacnyExpression(state, op.E)) {
          switch(op.Op) {
            case UnaryOpExpr.Opcode.Cardinality:
              if(!(result is IEnumerable)) {
                var resultExp = result is IVariable
                  ? Util.VariableToExpression(result as IVariable)
                  : result as Expression;
                yield return new UnaryOpExpr(op.tok, op.Op, resultExp);
              } else {
                var enumerator = result as IList;
                if(enumerator != null)
                  yield return new Dafny.LiteralExpr(op.tok, enumerator.Count);
              }
              yield break;
            case UnaryOpExpr.Opcode.Not:
              if(result is Dafny.LiteralExpr) {
                var lit = (Dafny.LiteralExpr)result;
                if(lit.Value is bool) {
                  // inverse the bool value
                  yield return new Dafny.LiteralExpr(op.tok, !(bool)lit.Value);
                } else {
                  Contract.Assert(false);
                  //TODO: error message
                }
              } else {
                var resultExp = result is IVariable ? Util.VariableToExpression(result as IVariable) : result as Expression;
                yield return new UnaryOpExpr(op.tok, op.Op, resultExp);
              }
              yield break;
            default:
              Contract.Assert(false, "Unsupported Unary Operator");
              yield break;
          }
        }
      } else if(expr is DisplayExpression) {
        var dexpr = (DisplayExpression)expr;
        if(dexpr.Elements.Count == 0) {
          yield return dexpr.Copy();
        } else {
          foreach(var item in ResolveDisplayExpression(state, dexpr)) {
            yield return item;
          }

        }
      } else { yield return expr; }
    }


    public static IEnumerable<IList<Expression>> ResolveDisplayExpression(ProofState state, DisplayExpression list) {
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentNullException>(list != null, "list");
      Contract.Ensures(Contract.Result<IEnumerable<IList<Expression>>>() != null);
      var dict = list.Elements.ToDictionary(element => element, element => EvaluateTacnyExpression(state, element));
      return GenerateList(dict, null);
    }


    private static IEnumerable<IList<Expression>> GenerateList(Dictionary<Expression, IEnumerable<object>> elements, IList<Expression> list) {
      Contract.Requires(elements != null);

      var tmp = list ?? new List<Expression>();
      var kvp = elements.FirstOrDefault();
      if (kvp.Equals(default(KeyValuePair<Expression, IEnumerable<Object>>))) {
        if (list != null)
          yield return list;
        else {
          yield return new List<Expression>();
        }
      } else {

        elements.Remove(kvp.Key);
        foreach (var result in kvp.Value) {
          var resultExpr = result is IVariable ? Util.VariableToExpression(result as IVariable) : result as Expression;
          tmp.Add(resultExpr);
          foreach (var value in GenerateList(elements, tmp)) {
            yield return value;
          }
        }
      }
    }

    public static IEnumerable<ProofState> EvaluateSuchThatStmt(AssignSuchThatStmt stmt, ProofState state) {
      var evaluator = new Atomic.SuchThatAtomic();
      return evaluator.Generate(stmt, state);
    }

    public static IEnumerable<ProofState> RegisterVariable(TacticVarDeclStmt declaration, ProofState state) {
      if (declaration.Update == null) yield break;
      var rhs = declaration.Update as UpdateStmt;
      if (rhs == null) {
        // check if rhs is SuchThatStmt
        if (declaration.Update is AssignSuchThatStmt) {
          foreach (var item in declaration.Locals)
            state.AddLocal(item, null);
          foreach (var item in EvaluateSuchThatStmt(declaration.Update as AssignSuchThatStmt, state)) {
            yield return item.Copy();
          }
        } else {
          foreach (var item in declaration.Locals)
            state.AddLocal(item, null);
        }
      } else {
        foreach(var item in rhs.Rhss) {
          int index = rhs.Rhss.IndexOf(item);
          Contract.Assert(declaration.Locals.ElementAtOrDefault(index) != null, "register var err");
          var exprRhs = item as ExprRhs;
          if(exprRhs?.Expr is ApplySuffix) {
            var aps = (ApplySuffix)exprRhs.Expr;
            foreach(var result in EvaluateTacnyExpression(state, aps)) {
              state.AddLocal(declaration.Locals[index], result);
            }
          } else if(exprRhs?.Expr is Dafny.LiteralExpr) {
            state.AddLocal(declaration.Locals[index], (Dafny.LiteralExpr)exprRhs?.Expr);
          } else if(exprRhs?.Expr is Dafny.NameSegment){
            var name = ((Dafny.NameSegment) exprRhs.Expr).Name;
            if(state.HasLocalValue(name))
            // in the case that referring to an exisiting tvar, dereference it
            state.AddLocal(declaration.Locals[index], state.GetLocalValue(name));
          } else {
            state.AddLocal(declaration.Locals[index], exprRhs?.Expr);
          }
        }
      }
      yield return state.Copy();
    }

    private static IEnumerable<ProofState> UpdateLocalValue(UpdateStmt us, ProofState state) {
      Contract.Requires<ArgumentNullException>(us != null, "stmt");
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentException>(state.IsLocalAssignment(us), "stmt");

      foreach (var item in us.Rhss) {
        int index = us.Rhss.IndexOf(item);
        Contract.Assert(us.Lhss.ElementAtOrDefault(index) != null, "register var err");
        var exprRhs = item as ExprRhs;
        if (exprRhs?.Expr is ApplySuffix) {
          var aps = (ApplySuffix)exprRhs.Expr;
          foreach (var result in EvaluateTacnyExpression(state, aps)) {
            state.UpdateLocal(((NameSegment)us.Lhss[index]).Name, result);
          }
        } else if (exprRhs?.Expr is Dafny.LiteralExpr) {
          state.UpdateLocal(((NameSegment)us.Lhss[index]).Name, (Dafny.LiteralExpr)exprRhs?.Expr);
        } else {
          state.UpdateLocal(((NameSegment)us.Lhss[index]).Name, exprRhs?.Expr);
        }
      }
      yield return state.Copy();
    }

    /// <summary>
    /// Insert the statement as is into the state
    /// </summary>
    /// <param name="stmt"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    private static IEnumerable<ProofState> DefaultAction(Statement stmt, ProofState state) {
      Contract.Requires<ArgumentNullException>(stmt != null, "stmt");
      Contract.Requires<ArgumentNullException>(state != null, "state");
      state.AddStatement(stmt);
      yield return state.Copy();
    }
  }
}
