using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System.Numerics;
using Tacny;

namespace LazyTacny {

  [ContractClass(typeof(AtomicLazyContract))]
  public interface IAtomicLazyStmt {
    IEnumerable<Solution> Resolve(Statement st, Solution solution);
  }

  [ContractClassFor(typeof(IAtomicLazyStmt))]
  // Validate the input before execution
  public abstract class AtomicLazyContract : IAtomicLazyStmt {

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      Contract.Requires<ArgumentNullException>(st != null && solution != null);
      yield break;

    }
  }

  public class Atomic {
    public readonly StaticContext StaticContext;
    public DynamicContext DynamicContext;
    private Strategy SearchStrat = Strategy.BFS;
    public bool IsFunction = false;

    public Atomic() {
      this.DynamicContext = new DynamicContext();
      this.StaticContext = new StaticContext();
    }
    protected Atomic(Atomic ac) {
      Contract.Requires(ac != null);

      this.DynamicContext = ac.DynamicContext;
      this.StaticContext = ac.StaticContext;
      this.SearchStrat = ac.SearchStrat;
    }

    public Atomic(MemberDecl md, ITactic tactic, UpdateStmt tac_call, Tacny.Program program) {
      Contract.Requires(md != null);
      Contract.Requires(tactic != null);

      this.DynamicContext = new DynamicContext(md, tactic, tac_call);
      this.StaticContext = new StaticContext(md, tac_call, program);
    }

    public Atomic(MemberDecl md, ITactic tac, UpdateStmt tac_call, StaticContext globalContext) {
      this.DynamicContext = new DynamicContext(md, tac, tac_call);
      this.StaticContext = globalContext;

    }

    public Atomic(DynamicContext localContext, StaticContext globalContext, Strategy searchStrategy, bool isFunction) {
      this.StaticContext = globalContext;
      this.DynamicContext = localContext.Copy();
      this.SearchStrat = searchStrategy;
      this.IsFunction = isFunction;
    }
    public void Initialize() {

    }

    /// <summary>
    /// Create a deep copy of an action
    /// </summary>
    /// <returns>Action</returns>
    public Atomic Copy() {
      Contract.Ensures(Contract.Result<Atomic>() != null);
      return new Atomic(DynamicContext, StaticContext, this.SearchStrat, IsFunction);
    }


    public static Solution ResolveTactic(ITactic tac, UpdateStmt tac_call, MemberDecl md, Tacny.Program tacnyProgram, List<IVariable> variables, List<IVariable> resolved) {
      Contract.Requires(tac != null);
      Contract.Requires(tac_call != null);
      Contract.Requires(md != null);
      Contract.Requires(tacnyProgram != null);
      Contract.Requires(tcce.NonNullElements<IVariable>(variables));
      Contract.Requires(tcce.NonNullElements<IVariable>(resolved));

      Atomic atomic = new Atomic(md, tac, tac_call, tacnyProgram);
      atomic.StaticContext.RegsiterGlobalVariables(variables, resolved);
      foreach (var result in ResolveTactic(atomic))
        return result;

      return null;
    }

    public static IEnumerable<Solution> ResolveTactic(Atomic atomic) {
      var tac = atomic.DynamicContext.tactic;
      if (tac is TacticFunction) {
        atomic.IsFunction = true;
        return ResolveTacticFunction(atomic);
      } else {
        // set strategy
        atomic.SearchStrat = SearchStrategy.GetSearchStrategy(tac);
        return ResolveTacticMethod(atomic);
      }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <param name="atomic"></param>
    /// <returns></returns>
    /*
        !!!Search strategies will go in here!!!
        !!! Validation of the results should also go here!!!
     */
    public static IEnumerable<Solution> ResolveTacticMethod(Atomic atomic, bool verify = true) {
      Contract.Requires(atomic.DynamicContext.tactic is Tactic);
      if (atomic.DynamicContext.tactic is Tactic) {
        ISearch searchStrategy = new SearchStrategy(atomic.SearchStrat);
        foreach (var item in searchStrategy.Search(atomic, verify))
          yield return item;
      }
      yield break;
    }

    public static IEnumerable<Solution> ResolveTacticFunction(Atomic atomic) {
      var tacFun = atomic.DynamicContext.tactic as TacticFunction;
      ExpressionTree expt = ExpressionTree.ExpressionToTree(tacFun.Body);
      foreach (var result in atomic.ResolveTacticFunction(expt)) {
        var ac = atomic.Copy();
        ac.DynamicContext.generatedExpressions.Add(result.TreeToExpression());
        yield return new Solution(ac);
      }
      yield break;
    }
    /// <summary>
    /// Resolve a block statement
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public IEnumerable<Solution> ResolveBody(BlockStmt body) {
      Contract.Requires<ArgumentNullException>(body != null);

      Debug.WriteLine("Resolving statement body");
      ISearch strat = new SearchStrategy(this.SearchStrat);
      Atomic ac = this.Copy();
      ac.DynamicContext.tacticBody = body.Body;
      ac.DynamicContext.ResetCounter();
      foreach (var item in strat.Search(ac, false)) {
        item.state.DynamicContext.tacticBody = this.DynamicContext.tacticBody; // set the body 
        item.state.DynamicContext.tac_call = this.DynamicContext.tac_call;
        item.state.DynamicContext.SetCounter(this.DynamicContext.GetCounter());
        yield return item;
      }
      Debug.WriteLine("Body resolved");

      yield break;
    }

    public static IEnumerable<Solution> ResolveStatement(Solution solution) {
      Contract.Requires<ArgumentNullException>(solution != null);
      if (solution.state.DynamicContext.IsResolved())
        yield break;
      // if no statements have been resolved, check the preconditions
      if (solution.state.DynamicContext.IsFirstStatment())
        TacnyContract.ValidateRequires(solution);
      // foreach result yield
      foreach (var result in solution.state.CallAction(solution.state.DynamicContext.GetCurrentStatement(), solution)) {

        result.parent = solution;
        // increment the counter if the statement has been fully resolved
        if (!result.state.DynamicContext.isPartialyResolved)
          result.state.DynamicContext.IncCounter();
        yield return result;

      }


      yield break;
    }

    protected IEnumerable<Solution> CallAction(object call, Solution solution) {
      foreach (var item in CallAtomic(call, solution)) {
        StaticContext.program.IncTotalBranchCount(StaticContext.program.currentDebug);
        yield return item;
      }

      yield break;
    }

    protected IEnumerable<Solution> CallAtomic(object call, Solution solution) {
      Contract.Requires<ArgumentNullException>(call != null);
      Contract.Requires<ArgumentNullException>(solution != null);

      System.Type type = null;
      Statement st = null;
      ApplySuffix aps = null;
      UpdateStmt us = null;
      if ((st = call as Statement) != null)
        type = StatementRegister.GetStatementType(st);
      else {
        if ((aps = call as ApplySuffix) != null) {
          type = StatementRegister.GetStatementType(aps);
          // convert applySuffix to an updateStmt
          st = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }
      }

      if (type != null) {
        Debug.WriteLine(String.Format("Resolving statement {0}", type.ToString()));
        var qq = Activator.CreateInstance(type, new object[] { this }) as IAtomicLazyStmt;
        if (qq != null) {
          foreach (var res in qq.Resolve(st, solution)) {
            yield return res;
          }
        } else {
          Contract.Assert(false, Util.Error.MkErr(st, 18, type.ToString(), typeof(IAtomicLazyStmt)));
        }
      } else { // could not determine the statement type, check if it's a nested tactic call

        Debug.WriteLine("Could not determine statement type");
        if (call is TacticVarDeclStmt) {
          Debug.WriteLine("Found tactic variable declaration");
          foreach (var item in (RegisterLocalVariable(call as TacticVarDeclStmt)))
            yield return item;
        } else if ((us = st as UpdateStmt) != null) {
          // if the statement is nested tactic call
          if (StaticContext.program.IsTacticCall(us)) {
            Debug.WriteLine("Found nested tactic call");

            var tactic = StaticContext.program.GetTactic(us);
            // TODO fix nested tactic calls
            ExprRhs er = us.Rhss[0] as ExprRhs;
            foreach (var result in ResolveNestedTacticCall(tactic, er.Expr as ApplySuffix)) {
              if (tactic is Tactic) {
                Atomic action = this.Copy();
                action.SetNewTarget(solution.state.GetNewTarget());
                foreach (var kvp in result.state.GetResult())
                  action.AddUpdated(kvp.Key, kvp.Value);
                yield return new Solution(action);
              }
            }
          } else if (IsLocalAssignment(us)) { // if the updatestmt is asignment

            foreach (var result in UpdateLocalVariable(us)) {
              yield return result;
            }
          } else if (IsArgumentApplication(us)) { // true when tactic is passed as an argument
            var name = GetNameSegment(us);
            var key = GetLocalKeyByName(name);
            var dafnyFormal = key as Dafny.Formal;
            if (dafnyFormal != null) {
              if (key.Type.ToString() == "Tactic") {
                var application = GetLocalValueByName(name) as ApplySuffix;
                var newUpdateStmt = new UpdateStmt(us.Tok, us.EndTok, us.Lhss, new List<AssignmentRhs>() { new ExprRhs(application) });
                var ac = this.Copy();
                foreach (var argument in application.Args) {
                  var ns = argument as NameSegment;
                  if (StaticContext.HasGlobalVariable(ns.Name)) {
                    var temp = StaticContext.GetGlobalVariable(ns.Name);
                    ac.DynamicContext.AddLocal(new Dafny.Formal(ns.tok, ns.Name, temp.Type, true, temp.IsGhost), ns);
                  }
                }
                foreach (var item in ac.CallAction(newUpdateStmt, solution)) {
                  yield return item;
                }
              } // other types go here
            } else {
              var value = GetLocalValueByName(name);
              aps = ((ExprRhs)us.Rhss[0]).Expr as ApplySuffix;
              var member = value as MemberDecl;
              if (member != null) {
                var newNs = new NameSegment(name.tok, member.Name, null);
                var expressionList = new List<Expression>();
                foreach (var arg in aps.Args) {
                  foreach (var result in ProcessStmtArgument(arg)) {
                    if (result is Expression)
                      expressionList.Add(result as Expression);
                    else if (result is IVariable)
                      expressionList.Add(IVariableToExpression(result as IVariable));
                    else {
                      Contract.Assert(false, "Sum tin wong");
                      break; // we assume that the the call returns only one expression
                    }
                  }
                }
                aps = new ApplySuffix(aps.tok, newNs, expressionList);
                Util.Printer.P.GetConsolePrinter().PrintExpression(aps, true);
                var newUs = new UpdateStmt(us.Tok, us.EndTok, us.Lhss, new List<AssignmentRhs>() { new ExprRhs(aps) });
                yield return AddNewStatement<UpdateStmt>(us, newUs);
              }

            }
          } else // insert the statement as is
            {
            yield return CallDefaultAction(st);
          }
        } else {
          yield return CallDefaultAction(st);
        }
      }
      yield break;
    }

    public IEnumerable<object> ProcessStmtArgument(Expression argument) {
      Contract.Requires<ArgumentNullException>(argument != null);
      NameSegment ns = null;
      ApplySuffix aps = null;
      if ((ns = argument as NameSegment) != null) {
        if (HasLocalWithName(ns))
          yield return GetLocalValueByName(ns.Name) ?? ns;
        else yield return ns;
      } else if ((aps = argument as ApplySuffix) != null) {

        var type = StatementRegister.GetAtomicType(aps);
        // if apply suffix is an atomic
        if (type != StatementRegister.Atomic.UNDEFINED) {
          UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
          // create a unique local variable
          Dafny.LocalVariable lv = new Dafny.LocalVariable(aps.tok, aps.tok, aps.Lhs.tok.val, new BoolType(), false);
          TacticVarDeclStmt tvds = new TacticVarDeclStmt(us.Tok, us.EndTok, new List<Dafny.LocalVariable>() { lv }, us);
          foreach (var item in CallAction(tvds, new Solution(this.Copy()))) {
            var res = item.state.DynamicContext.localDeclarations[lv];
            item.state.DynamicContext.localDeclarations.Remove(lv);
            yield return res;
          }
        } else {
          if (aps.Lhs is ExprDotName) {
            foreach (var solution in ProcessStmtArgument(aps.Lhs)) {
              yield return new ApplySuffix(aps.tok, solution as Expression, aps.Args);
            }
          } else if (IsArgumentApplication(aps)) {


          } else { yield return argument; }
        }
        //UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        //// create a unique local variable
        //Dafny.LocalVariable lv = new Dafny.LocalVariable(aps.tok, aps.tok, aps.Lhs.tok.val, new BoolType(), false);
        //TacticVarDeclStmt tvds = new TacticVarDeclStmt(us.Tok, us.EndTok, new List<Dafny.LocalVariable>() { lv }, us);
        //foreach (var item in CallAction(tvds, new Solution(this.Copy()))) {
        //    var res = item.state.DynamicContext.localDeclarations[lv];
        //    item.state.DynamicContext.localDeclarations.Remove(lv);
        //    yield return res;
        //}
      } else if (argument is TacnyBinaryExpr) {
        var newAps = new ApplySuffix(CreateToken("TacnyBinaryExpr", 0, 0), argument, new List<Expression>()); // create a wrapper for the TacnyBinaryExpr
        foreach (var result in CallAction(newAps, new Solution(this.Copy()))) {
          yield return result.state.DynamicContext.generatedExpressions[0]; // we assume that BinaryExpr only generates one expression
        }
      } else if (argument is BinaryExpr || argument is ParensExpression) {
        ExpressionTree expt = ExpressionTree.ExpressionToTree(argument);
        foreach (var result in ResolveExpression(expt)) {
          //if (IsResolvable(expt))
          //  yield return EvaluateExpression(expt);
          //else
          yield return expt.TreeToExpression();
        }
      } else if (argument is ExprDotName) {
        var edn = argument as ExprDotName;
        if (HasLocalWithName(edn.Lhs as NameSegment)) {
          ns = edn.Lhs as NameSegment;
          var newLhs = GetLocalValueByName(ns.Name) ?? ns;
          yield return new ExprDotName(edn.tok, newLhs as Expression, edn.SuffixName, edn.OptTypeArguments);
        } else {
          yield return edn;
        }
      } else {
        yield return argument;
      }
      yield break;
    }

    private IEnumerable<Solution> RegisterLocalVariable(TacticVarDeclStmt declaration) {
      Contract.Requires(declaration != null);
      // if declaration has rhs
      if (declaration.Update != null) {
        UpdateStmt rhs = declaration.Update as UpdateStmt;
        // if statement is of type var q;
        if (rhs == null) { /* leave the statement */ } else {
          foreach (var item in rhs.Rhss) {
            int index = rhs.Rhss.IndexOf(item);
            Contract.Assert(declaration.Locals.ElementAtOrDefault(index) != null, Util.Error.MkErr(declaration, 8));
            ExprRhs exprRhs = item as ExprRhs;
            // if the declaration is literal expr (e.g. tvar q := 1)
            Dafny.LiteralExpr litExpr = exprRhs.Expr as Dafny.LiteralExpr;
            if (litExpr != null) {
              AddLocal(declaration.Locals[index], litExpr);
              yield return new Solution(this.Copy());
            } else {
              var aps = exprRhs.Expr as ApplySuffix;
              // if apply suffix is an atomic
              if (StaticContext.program.IsTacticCall(aps)) {
                var tactic = StaticContext.program.GetTactic(aps);
                // resolve the nested tactic call
                foreach (var result in ResolveNestedTacticCall(tactic, aps)) {
                  if (tactic is TacticFunction) {
                    var resultExpressions = result.state.DynamicContext.generatedExpressions;
                    if (resultExpressions.Count == 0)
                      yield return AddNewLocal<object>(declaration.Locals[index], null);
                    else {
                      Util.Printer.P.GetConsolePrinter().PrintExpression(resultExpressions[0], false);
                      Console.Out.WriteLine("");
                      yield return AddNewLocal(declaration.Locals[index], resultExpressions[0]); // we only expect a function to return a single expression
                    }
                  } else if (tactic is Tactic) {
                    var resultStatements = result.state.GetResolved();
                    if (resultStatements.Count == 0)
                      yield return AddNewLocal<object>(declaration.Locals[index], null);
                    else {
                      Util.Printer.P.GetConsolePrinter().PrintStatement(resultStatements[0], 0);
                      Console.Out.WriteLine("");
                      yield return AddNewLocal(declaration.Locals[index], resultStatements[0]); // we only expect a function to return a single expression
                    }
                  }
                }

              } else {
                foreach (var result in ProcessStmtArgument(exprRhs.Expr)) {
                  yield return AddNewLocal(declaration.Locals[index], result);
                }
              }
            }
          }
        }
      } else {
        foreach (var item in declaration.Locals)
          AddLocal(item as IVariable, null);
        yield return new Solution(this.Copy());

      }
      yield break;
    }

    private IEnumerable<Solution> ResolveNestedTacticCall(ITactic tactic, ApplySuffix aps) {

      UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
      var atomic = new Atomic(DynamicContext.md, tactic, us, StaticContext);
      //// register local variables
      List<Expression> exps = aps.Args;
      Contract.Assert(exps.Count == atomic.DynamicContext.tactic.Ins.Count);
      atomic.SetNewTarget(GetNewTarget());
      for (int i = 0; i < exps.Count; i++) {
        foreach (var result in ProcessStmtArgument(exps[i])) {
          atomic.AddLocal(atomic.DynamicContext.tactic.Ins[i], result);
        }
      }
      return ResolveTactic(atomic);
    }

    private IEnumerable<Solution> UpdateLocalVariable(UpdateStmt updateStmt) {
      Contract.Requires(updateStmt != null);
      Debug.WriteLine("Updating local variable");
      // if statement is of type var q;
      Contract.Assert(updateStmt.Lhss.Count == updateStmt.Rhss.Count, Util.Error.MkErr(updateStmt, 8));
      for (int i = 0; i < updateStmt.Lhss.Count; i++) {
        NameSegment variable = updateStmt.Lhss[i] as NameSegment;
        Contract.Assert(variable != null, Util.Error.MkErr(updateStmt, 5, typeof(NameSegment), updateStmt.Lhss[i].GetType()));
        Contract.Assert(HasLocalWithName(variable), Util.Error.MkErr(updateStmt, 9, variable.Name));
        // get the key of the variable
        IVariable local = GetLocalKeyByName(variable);
        foreach (var item in updateStmt.Rhss) {
          // unfold the rhs
          ExprRhs exprRhs = item as ExprRhs;
          if (exprRhs != null) {
            // if the expression is a literal value update the value
            Dafny.LiteralExpr litVal = exprRhs.Expr as Dafny.LiteralExpr;
            if (litVal != null) {
              AddLocal(local, litVal);
              yield return new Solution(this.Copy());
            } else { // otherwise process the expression
              foreach (var result in ProcessStmtArgument(exprRhs.Expr)) {
                AddLocal(local, result);
                yield return new Solution(this.Copy());
              }
            }
          }
        }
      }

      yield break;
    }

    /// <summary>
    /// Clear local variables, and fill them with tactic arguments. Use with caution.
    /// </summary>
    public void FillTacticInputs() {
      DynamicContext.FillTacticInputs();
    }

    protected void InitArgs(Statement st, out List<Expression> call_arguments) {
      Contract.Requires(st != null);
      Contract.Ensures(Contract.ValueAtReturn<List<Expression>>(out call_arguments) != null);
      IVariable lv;
      InitArgs(st, out lv, out call_arguments);
    }

    /// <summary>
    /// Extract statement arguments and local variable definition
    /// </summary>
    /// <param name="st">Atomic statement</param>
    /// <param name="lv">Local variable</param>
    /// <param name="call_arguments">List of arguments</param>
    /// <returns>Error message</returns>
    protected void InitArgs(Statement st, out IVariable lv, out List<Expression> call_arguments) {
      Contract.Requires(st != null);
      Contract.Ensures(Contract.ValueAtReturn<List<Expression>>(out call_arguments) != null);
      lv = null;
      call_arguments = null;
      TacticVarDeclStmt tvds = null;
      UpdateStmt us = null;
      TacnyBlockStmt tbs = null;
      // tacny variables should be declared as tvar or tactic var
      if (st is VarDeclStmt)
        Contract.Assert(false, Util.Error.MkErr(st, 13));

      if ((tvds = st as TacticVarDeclStmt) != null) {
        lv = tvds.Locals[0];
        call_arguments = GetCallArguments(tvds.Update as UpdateStmt);

      } else if ((us = st as UpdateStmt) != null) {
        if (us.Lhss.Count == 0)
          call_arguments = GetCallArguments(us);
        else {
          NameSegment ns = (NameSegment)us.Lhss[0];
          if (HasLocalWithName(ns)) {
            lv = GetLocalKeyByName(ns);
            call_arguments = GetCallArguments(us);
          } else
            Util.Printer.Error(st, "Local variable {0} is not declared", ns.Name);
        }
      } else if ((tbs = st as TacnyBlockStmt) != null) {
        ParensExpression pe = tbs.Guard as ParensExpression;
        if (pe != null)
          call_arguments = new List<Expression>() { pe.E };
        else
          call_arguments = new List<Expression>() { tbs.Guard };
      } else
        Util.Printer.Error(st, "Wrong number of method result arguments; Expected {0} got {1}", 1, 0);

    }

    private Solution CallDefaultAction(Statement st) {
      Contract.Requires(st != null);

      Atomic state = this.Copy();
      state.AddUpdated(st, st);
      return new Solution(state, null);
    }

    /// <summary>
    /// Find closest while statement to the tactic call
    /// </summary>
    /// <param name="tac_stmt">Tactic call</param>
    /// <param name="member">Method</param>
    /// <returns>WhileStmt</returns>
    protected static WhileStmt FindWhileStmt(Statement tac_stmt, MemberDecl member) {
      Contract.Requires(tac_stmt != null);
      Contract.Requires(member != null);

      Method m = (Method)member;

      int index = m.Body.Body.IndexOf(tac_stmt);
      if (index <= 0)
        return null;

      while (index >= 0) {
        Statement stmt = m.Body.Body[index];

        WhileStmt ws = stmt as WhileStmt;
        if (ws != null)
          return ws;

        index--;
      }
      return null;
    }
    #region getters
    protected static List<Expression> GetCallArguments(UpdateStmt us) {
      Contract.Requires(us != null);
      ExprRhs er = (ExprRhs)us.Rhss[0];
      return ((ApplySuffix)er.Expr).Args;
    }
    [Pure]
    protected bool HasLocalWithName(NameSegment ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return DynamicContext.HasLocalWithName(ns);
    }

    [Pure]
    protected object GetLocalValueByName(NameSegment ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return DynamicContext.GetLocalValueByName(ns.Name);
    }

    [Pure]
    protected object GetLocalValueByName(IVariable variable) {
      Contract.Requires<ArgumentNullException>(variable != null);
      return DynamicContext.GetLocalValueByName(variable);
    }

    [Pure]
    protected object GetLocalValueByName(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      return DynamicContext.GetLocalValueByName(name);
    }

    [Pure]
    protected IVariable GetLocalKeyByName(NameSegment ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return DynamicContext.GetLocalKeyByName(ns.Name);
    }

    [Pure]
    protected IVariable GetLocalKeyByName(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      return DynamicContext.GetLocalKeyByName(name);
    }

    public void Fin() {
      StaticContext.resolved.Clear();
      StaticContext.resolved.AddRange(DynamicContext.generatedStatements.Values.ToArray());
      StaticContext.newTarget = DynamicContext.newTarget;
    }

    public void AddLocal(IVariable lv, object value) {
      Contract.Requires<ArgumentNullException>(lv != null);
      DynamicContext.AddLocal(lv, value);
    }

    protected static Token CreateToken(string val, int line, int col) {
      var tok = new Token(line, col);
      tok.val = val;
      return tok;
    }
    [Pure]
    public List<Statement> GetResolved() {
      return StaticContext.resolved;
    }
    [Pure]
    public UpdateStmt GetTacticCall() {
      return DynamicContext.tac_call;
    }

    public void AddUpdated(Statement key, Statement value) {
      Contract.Requires(key != null && value != null);
      DynamicContext.AddUpdated(key, value);
    }

    public void RemoveUpdated(Statement key) {
      Contract.Requires(key != null);
      DynamicContext.RemoveUpdated(key);
    }

    public Statement GetUpdated(Statement key) {
      Contract.Requires(key != null);
      return DynamicContext.GetUpdated(key);
    }

    public List<Statement> GetAllUpdated() {
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return DynamicContext.GetAllUpdated();
    }

    public Dictionary<Statement, Statement> GetResult() {
      return DynamicContext.generatedStatements;
    }

    public MemberDecl GetNewTarget() {
      return DynamicContext.newTarget;
    }
    #endregion
    public void SetNewTarget(MemberDecl new_target) {
      DynamicContext.newTarget = new_target;
    }
    /// <summary>
    /// Creates a new tactic from a given tactic body and updates the context
    /// </summary>
    /// <param name="tac"></param>
    /// <param name="newBody"></param>
    /// <param name="decCounter"></param>
    /// <returns></returns>
    //protected Solution CreateTactic(List<Statement> newBody, bool decCounter = true)
    //{
    //    Contract.Ensures(Contract.Result<Solution>() != null);
    //    Tactic tac = dynamicContext.tactic;
    //    Tactic newTac = new Tactic(tac.tok, tac.Name, tac.HasStaticKeyword,
    //                                tac.TypeArgs, tac.Ins, tac.Outs, tac.Req, tac.Mod, tac.Ens,
    //                                tac.Decreases, new BlockStmt(tac.Body.Tok, tac.Body.EndTok, newBody),
    //                                tac.Attributes, tac.SignatureEllipsis);
    //    Atomic newAtomic = this.Copy();
    //    newAtomic.dynamicContext.tactic = newTac;
    //    newAtomic.dynamicContext.tacticBody = newBody;
    //    /* HACK */
    //    // decrase the tactic body counter
    //    // so the interpreter would execute newly inserted atomic
    //    if (decCounter)
    //        newAtomic.dynamicContext.DecCounter();
    //    return new Solution(newAtomic);
    //}

    /// <summary>
    /// Replaces the statement at current body counter with a new statement
    /// </summary>
    /// <param name="oldBody"></param>
    /// <param name="newStatement"></param>
    /// <returns></returns>
    protected List<Statement> ReplaceCurrentAtomic(Statement newStatement) {
      Contract.Requires(newStatement != null);
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      int index = DynamicContext.GetCounter();
      List<Statement> newBody = DynamicContext.GetFreshTacticBody();
      newBody[index] = newStatement;
      return newBody;
    }

    protected List<Statement> ReplaceCurrentAtomic(List<Statement> list) {
      Contract.Requires(list != null);
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      int index = DynamicContext.GetCounter();
      List<Statement> newBody = DynamicContext.GetFreshTacticBody();
      newBody.RemoveAt(index);
      newBody.InsertRange(index, list);
      return newBody;
    }

    protected Expression EvaluateExpression(ExpressionTree expt) {
      Contract.Requires(expt != null);
      if (expt.IsLeaf()) {
        return EvaluateLeaf(expt) as Dafny.LiteralExpr;
      } else {
        BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
        if (BinaryExpr.IsEqualityOp(bexp.Op)) {
          var boolVal = EvaluateEqualityExpression(expt);
          return new Dafny.LiteralExpr(new Token(), boolVal);
        } else {
          Dafny.LiteralExpr lhs = EvaluateExpression(expt.lChild) as Dafny.LiteralExpr;
          Dafny.LiteralExpr rhs = EvaluateExpression(expt.rChild) as Dafny.LiteralExpr;
          // for now asume lhs and rhs are integers
          BigInteger l = (BigInteger)lhs.Value;
          BigInteger r = (BigInteger)rhs.Value;

          BigInteger res = 0;


          switch (bexp.Op) {
            case BinaryExpr.Opcode.Sub:
              res = BigInteger.Subtract(l, r);
              break;
            case BinaryExpr.Opcode.Add:
              res = BigInteger.Add(l, r);
              break;
            case BinaryExpr.Opcode.Mul:
              res = BigInteger.Multiply(l, r);
              break;
            case BinaryExpr.Opcode.Div:
              res = BigInteger.Divide(l, r);
              break;
          }

          return new Dafny.LiteralExpr(lhs.tok, res);
        }
      }
    }

    /// <summary>
    /// Evalutate an expression tree
    /// </summary>
    /// <param name="expt"></param>
    /// <returns></returns>
    public bool EvaluateEqualityExpression(ExpressionTree expt) {
      Contract.Requires(expt != null);
      // if the node is leaf, cast it to bool and return
      if (expt.IsLeaf()) {
        Dafny.LiteralExpr lit = EvaluateLeaf(expt) as Dafny.LiteralExpr;
        return lit.Value is bool ? (bool)lit.Value : false;
      }
      // left branch only
      else if (expt.lChild != null && expt.rChild == null)
        return EvaluateEqualityExpression(expt.lChild);
      // if there is no more nesting resolve the expression
      else if (expt.lChild.IsLeaf() && expt.rChild.IsLeaf()) {
        Dafny.LiteralExpr lhs = null;
        Dafny.LiteralExpr rhs = null;
        lhs = EvaluateLeaf(expt.lChild) as Dafny.LiteralExpr;
        rhs = EvaluateLeaf(expt.rChild) as Dafny.LiteralExpr;
        if (!lhs.GetType().Equals(rhs.GetType()))
          return false;
        BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
        int res = -1;
        if (lhs.Value is BigInteger) {
          BigInteger l = (BigInteger)lhs.Value;
          BigInteger r = (BigInteger)rhs.Value;
          res = l.CompareTo(r);
        } else if (lhs.Value is string) {
          string l = lhs.Value as string;
          string r = rhs.Value as string;
          res = l.CompareTo(r);
        } else if (lhs.Value is bool) {
          res = ((bool)lhs.Value).CompareTo((bool)rhs.Value);
        }

        if (bexp.Op == BinaryExpr.Opcode.Eq)
          return res == 0;
        else if (bexp.Op == BinaryExpr.Opcode.Neq)
          return res != 0;
        else if (bexp.Op == BinaryExpr.Opcode.Ge)
          return res >= 0;
        else if (bexp.Op == BinaryExpr.Opcode.Gt)
          return res > 0;
        else if (bexp.Op == BinaryExpr.Opcode.Le)
          return res <= 0;
        else if (bexp.Op == BinaryExpr.Opcode.Lt)
          return res < 0;
      } else { // evaluate a nested expression

        BinaryExpr bexp = tcce.NonNull<BinaryExpr>(expt.data as BinaryExpr);
        if (bexp.Op == BinaryExpr.Opcode.And)
          return EvaluateEqualityExpression(expt.lChild) && EvaluateEqualityExpression(expt.rChild);
        else if (bexp.Op == BinaryExpr.Opcode.Or)
          return EvaluateEqualityExpression(expt.lChild) || EvaluateEqualityExpression(expt.rChild);
      }
      return false;
    }

    protected IEnumerable<ExpressionTree> ResolveTacticFunction(ExpressionTree body) {
      Contract.Requires(body != null);

      var leafs = body.TreeToList();
      var leafResolvers = new Dictionary<ExpressionTree, IEnumerable<object>>();

      leafResolvers.Add(body, ProcessStmtArgument(body.TreeToExpression()));

      return GenerateExpressionTree(body, leafResolvers);
    }



    protected IEnumerable<ExpressionTree> ResolveExpression(ExpressionTree expression) {
      var leafs = expression.GetLeafs();
      var leafResolvers = new Dictionary<ExpressionTree, IEnumerable<object>>();
      foreach (var leaf in leafs)
        leafResolvers.Add(leaf, ProcessStmtArgument(leaf.TreeToExpression()));

      return GenerateExpressionTree(expression, leafResolvers);
    }

    /// <summary>
    /// Resolve Tacny level expressions
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>

    protected IEnumerable<ExpressionTree> GenerateExpressionTree(ExpressionTree tree, Dictionary<ExpressionTree, IEnumerable<object>> leafResolvers) {
      var kvp = leafResolvers.FirstOrDefault();

      if (kvp.Equals(default(KeyValuePair<ExpressionTree, IEnumerable<object>>))) {
        yield return tree;
        yield break;
      }

      leafResolvers.Remove(kvp.Key);
      foreach (var value in kvp.Value) {
        Expression newValue = null;
        if (value is Expression)
          newValue = value as Expression;
        else if (value is IVariable)
          newValue = IVariableToExpression(value as IVariable);
        else
          Contract.Assert(false, "Sum tin wong");
        var newLeaf = ExpressionTree.ExpressionToTree(newValue);
        var newTree = ExpressionTree.FindAndReplaceNode(tree, newLeaf, kvp.Key);

        //Util.Printer.P.GetConsolePrinter().PrintExpression(newTree.TreeToExpression(), false);


        foreach (var result in GenerateExpressionTree(newTree, leafResolvers))
          yield return result;
      }

      yield break;
    }

    /// <summary>
    /// Evaluate a leaf node
    /// TODO: support for call evaluation
    /// </summary>
    /// <param name="expt"></param>
    /// <returns></returns>
    protected object EvaluateLeaf(ExpressionTree expt) {
      Contract.Requires(expt != null && expt.IsLeaf());
      if (expt.data is NameSegment || expt.data is ApplySuffix) {
        // fix me
        foreach (var item in ProcessStmtArgument(expt.data))
          return item;
      } else if (expt.data is Dafny.LiteralExpr)
        return expt.data;

      return null;
    }

    /// <summary>
    /// Resolve all variables in expression to either literal values
    /// or to orignal declared nameSegments
    /// </summary>
    /// <param name="guard"></param>
    /// <returns></returns>
    protected void ResolveExpression(ref ExpressionTree guard) {
      Contract.Requires(guard != null);
      if (guard.IsLeaf()) {
        Expression newNs; // potential encapsulation problems
        var result = EvaluateLeaf(guard);

        // we only need to replace nameSegments
        if (guard.data is NameSegment) {
          Contract.Assert(result != null);
          if (result is MemberDecl) {
            MemberDecl md = result as MemberDecl;
            newNs = new Dafny.StringLiteralExpr(new Token(), md.Name, true);
          } else if (result is Dafny.Formal) {
            var tmp = result as Dafny.Formal;
            newNs = new NameSegment(tmp.tok, tmp.Name, null);
          } else if (result is NameSegment) {
            newNs = result as NameSegment;
          } else {
            newNs = result as Expression;// Dafny.LiteralExpr;
          }
          guard.data = newNs;
        }
      } else {
        ResolveExpression(ref guard.lChild);
        if (guard.rChild != null)
          ResolveExpression(ref guard.rChild);
      }
    }
    /// <summary>
    /// Determine whehter the given expression tree can be evaluated.
    /// THe value is true if all leaf nodes have literal values
    /// </summary>
    /// <param name="expt"></param>
    /// <returns></returns>
    [Pure]
    protected bool IsResolvable(ExpressionTree expt) {
      Contract.Requires(expt.isRoot());
      List<Expression> leafs = expt.GetLeafData();

      foreach (var leaf in leafs) {
        if (leaf is NameSegment) {
          NameSegment ns = leaf as NameSegment;
          object local = GetLocalValueByName(ns);
          if (!(local is Dafny.LiteralExpr))
            return false;
        } else if (!(leaf is Dafny.LiteralExpr)) {
          if (leaf is ApplySuffix) {
            if (StaticContext.program.IsTacticCall(leaf as ApplySuffix) || IsArgumentApplication(leaf as ApplySuffix)) {
              foreach (var result in ProcessStmtArgument(leaf)) {
                Util.Printer.P.GetConsolePrinter().PrintExpression(result as Expression, false);
              }
            }
          }
        } else {
          return false;
        }
      }
      return true;
    }

    public Solution AddNewStatement<T>(T oldValue, T newValue) where T : Statement {
      var ac = this.Copy();
      ac.AddUpdated(oldValue, newValue);
      return new Solution(ac);
    }

    public Solution AddNewLocal<T>(IVariable variable, T value) where T : class {
      var ac = this.Copy();
      ac.AddLocal(variable, value);
      return new Solution(ac);
    }

    /// <summary>
    /// Generate a Dafny program and verify it
    /// </summary>
    public bool ResolveAndVerify(Solution solution) {
      Contract.Requires<ArgumentNullException>(solution != null);

      Dafny.Program prog = StaticContext.program.ParseProgram();
      solution.GenerateProgram(ref prog);
      StaticContext.program.ClearBody(DynamicContext.md);
#if !DEBUG
            staticContext.program.PrintMember(prog, solution.state.staticContext.md.Name);
#endif
      if (!StaticContext.program.ResolveProgram())
        return false;
      StaticContext.program.VerifyProgram();
      return true;

    }



    /// <summary>
    /// Register datatype ctor vars as locals
    /// </summary>
    /// <param name="datatype"></param>
    /// <param name="index"></param>
    public void RegisterLocals(DatatypeDecl datatype, int index, Dictionary<string, Dafny.Type> ctorTypes = null) {
      Contract.Requires(datatype != null);
      Contract.Requires(index + 1 <= datatype.Ctors.Count);

      foreach (var formal in datatype.Ctors[index].Formals) {
        // register globals as name segments
        // registed the ctor argument with the correct type
        if (ctorTypes != null) {
          UserDefinedType udt = formal.Type as UserDefinedType;
          if (udt != null) {
            if (ctorTypes.ContainsKey(udt.Name)) {
              Dafny.Formal newFormal = new Dafny.Formal(formal.Tok, formal.Name, ctorTypes[udt.Name], formal.InParam, formal.IsGhost);
              StaticContext.RegsiterGlobalVariable(newFormal);
            } else {
              StaticContext.RegsiterGlobalVariable(formal);
            }
          } else {
            StaticContext.RegsiterGlobalVariable(formal);
          }

        } else
          StaticContext.RegsiterGlobalVariable(formal);
      }
    }

    /// <summary>
    /// Remove datatype ctor vars from locals
    /// </summary>
    /// <param name="datatype"></param>
    /// <param name="index"></param>
    public void RemoveLocals(DatatypeDecl datatype, int index) {
      Contract.Requires(datatype != null);
      Contract.Requires(index + 1 <= datatype.Ctors.Count);
      foreach (var formal in datatype.Ctors[index].Formals) {
        // register globals as name segments
        StaticContext.RemoveGlobalVariable(formal);
      }
    }

    [Pure]
    protected static bool IsLocalAssignment(UpdateStmt us) {
      return us.Lhss.Count > 0 && us.Rhss.Count > 0;
    }

    [Pure]
    protected bool IsArgumentApplication(UpdateStmt us) {
      Contract.Requires(us != null);
      var nameSegment = GetNameSegment(us);
      return this.IsArgumentApplication(nameSegment);
    }

    [Pure]
    protected bool IsArgumentApplication(ApplySuffix aps) {
      Contract.Requires(aps != null);
      var nameSegment = GetNameSegment(aps);
      return this.IsArgumentApplication(nameSegment);
    }

    [Pure]
    protected bool IsArgumentApplication(NameSegment ns) {
      Contract.Requires(ns != null);
      return this.DynamicContext.HasLocalWithName(ns);
    }

    [Pure]
    public static Expression IVariableToExpression(IVariable variable) {
      Contract.Requires(variable != null);
      return new NameSegment(variable.Tok, variable.Name, null);
    }

    [Pure]
    protected static NameSegment GetNameSegment(UpdateStmt us) {
      Contract.Requires(us != null);
      ExprRhs rhs = us.Rhss[0] as ExprRhs;
      if (rhs == null) {
        return null;
      }
      return GetNameSegment(rhs.Expr as ApplySuffix);
    }

    [Pure]
    protected static NameSegment GetNameSegment(ApplySuffix aps) {
      return ((aps != null) ? (aps.Lhs as NameSegment) : null);
    }

    [Pure]
    protected Dafny.LocalVariable GenerateFreshLocalVariable(NameSegment ns) {
      int num = this.DynamicContext.localDeclarations.Count<KeyValuePair<IVariable, object>>(i => i.Key.Name == ns.Name);
      return new Dafny.LocalVariable(ns.tok, ns.tok, string.Format("{0}_{1}", ns.Name, num), new ObjectType(), true);
    }

  }
}
