using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Tacny;
using Util;
using Formal = Microsoft.Dafny.Formal;
using LiteralExpr = Microsoft.Dafny.LiteralExpr;
using LocalVariable = Microsoft.Dafny.LocalVariable;
using Printer = Util.Printer;
using Program = Tacny.Program;
using QuantifierExpr = Microsoft.Dafny.QuantifierExpr;
using Type = Microsoft.Dafny.Type;

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
    private Strategy _searchStrat = Strategy.Bfs;
    public bool IsFunction;

    public Atomic() {
      DynamicContext = new DynamicContext();
      StaticContext = new StaticContext();
    }
    protected Atomic(Atomic ac) {
      Contract.Requires(ac != null);

      DynamicContext = ac.DynamicContext;
      StaticContext = ac.StaticContext;
      _searchStrat = ac._searchStrat;
    }

    public Atomic(MemberDecl md, ITactic tactic, UpdateStmt tacCall, Program program) {
      Contract.Requires(md != null);
      Contract.Requires(tactic != null);

      DynamicContext = new DynamicContext(md, tactic, tacCall);
      StaticContext = new StaticContext(md, tacCall, program);
    }

    public Atomic(MemberDecl md, ITactic tac, UpdateStmt tacCall, StaticContext globalContext) {
      DynamicContext = new DynamicContext(md, tac, tacCall);
      StaticContext = globalContext;

    }

    public Atomic(DynamicContext localContext, StaticContext globalContext, Strategy searchStrategy, bool isFunction) {
      StaticContext = globalContext;
      DynamicContext = localContext.Copy();
      _searchStrat = searchStrategy;
      IsFunction = isFunction;
    }
    public void Initialize() {

    }

    /// <summary>
    /// Create a deep copy of an action
    /// </summary>
    /// <returns>Action</returns>
    public Atomic Copy() {
      Contract.Ensures(Contract.Result<Atomic>() != null);
      return new Atomic(DynamicContext, StaticContext, _searchStrat, IsFunction);
    }


    public static Solution ResolveTactic(UpdateStmt tacticCall, MemberDecl md, Program tacnyProgram, List<IVariable> variables, List<IVariable> resolved, WhileStmt ws = null) {
      Contract.Requires(tacticCall != null);
      Contract.Requires(md != null);
      Contract.Requires(tacnyProgram != null);
      Contract.Requires(tcce.NonNullElements(variables));
      Contract.Requires(tcce.NonNullElements(resolved));

      var tactic = tacnyProgram.GetTactic(tacticCall);
      tacnyProgram.SetCurrent(tactic, md);
      Console.Out.WriteLine($"Resolving {tactic.Name} in {md.Name}");

      var atomic = new Atomic(md, tactic, tacticCall, tacnyProgram);
      atomic.StaticContext.RegsiterGlobalVariables(variables, resolved);
      atomic.DynamicContext.whileStmt = ws;
      atomic.ResolveTacticArguments(atomic);

      return ResolveTactic(atomic).FirstOrDefault();
    }

    public static IEnumerable<Solution> ResolveTactic(Atomic atomic, bool verify = true) {
      Contract.Requires(atomic != null);
      var tac = atomic.DynamicContext.tactic;
      if (tac is TacticFunction) {
        atomic.IsFunction = true;
        return ResolveTacticFunction(atomic);
      }
      // set strategy
      atomic._searchStrat = SearchStrategy.GetSearchStrategy(tac);
      return ResolveTacticMethod(atomic, verify);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="atomic"></param>
    /// <param name="verify"></param>
    /// <returns></returns>
    /*
        !!!Search strategies will go in here!!!
        !!! Validation of the results should also go here!!!
     */
    public static IEnumerable<Solution> ResolveTacticMethod(Atomic atomic, bool verify = true) {
      Contract.Requires(atomic != null);
      Contract.Requires(atomic.DynamicContext.tactic is Tactic);
      TacnyContract.ValidateRequires(atomic);
      ISearch searchStrategy = new SearchStrategy(atomic._searchStrat);
      return searchStrategy.Search(atomic, verify);
    }

    public static IEnumerable<Solution> ResolveTacticFunction(Atomic atomic) {
      Contract.Requires(atomic.DynamicContext.tactic is TacticFunction);
      var tacFun = atomic.DynamicContext.tactic as TacticFunction;
      if (tacFun == null) yield break;
      var expt = ExpressionTree.ExpressionToTree(tacFun.Body);
      foreach (var result in atomic.ResolveTacticFunction(expt)) {
        var ac = atomic.Copy();
        ac.DynamicContext.generatedExpressions.Add(result.TreeToExpression());
        yield return new Solution(ac);
      }
    }
    /// <summary>
    /// Resolve a block statement
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public IEnumerable<Solution> ResolveBody(BlockStmt body) {
      Contract.Requires<ArgumentNullException>(body != null);

      Debug.WriteLine("Resolving statement body");
      ISearch strat = new SearchStrategy(_searchStrat);
      var ac = Copy();
      ac.DynamicContext.tacticBody = body.Body;
      ac.DynamicContext.ResetCounter();
      // hack: make sure statements generated by parent body are not passed to child
      // this fucks things up
      //ac.DynamicContext.generatedStatements = new Dictionary<Statement, Statement>();
      foreach (var item in strat.Search(ac, false)) {
        item.State.DynamicContext.tacticBody = DynamicContext.tacticBody; // set the body 
        item.State.DynamicContext.tac_call = DynamicContext.tac_call;
        item.State.DynamicContext.SetCounter(DynamicContext.GetCounter());
        yield return item;
      }
      Debug.WriteLine("Body resolved");
    }

    public static IEnumerable<Solution> ResolveStatement(Solution solution) {
      Contract.Requires<ArgumentNullException>(solution != null);
      if (solution.State.DynamicContext.IsResolved())
        yield break;
      foreach (var result in solution.State.CallAction(solution.State.DynamicContext.GetCurrentStatement(), solution)) {

        result.Parent = solution;
        // increment the counter if the statement has been fully resolved
        if (!result.State.DynamicContext.isPartialyResolved)
          result.State.DynamicContext.IncCounter();
        yield return result;

      }
    }

    protected IEnumerable<Solution> CallAction(object call, Solution solution) {
      foreach (var item in CallAtomic(call, solution)) {
        StaticContext.program.IncTotalBranchCount(StaticContext.program.CurrentDebug);
        yield return item;
      }
    }

    protected IEnumerable<Solution> CallAtomic(object call, Solution solution) {
      Contract.Requires<ArgumentNullException>(call != null);
      Contract.Requires<ArgumentNullException>(solution != null);

      System.Type type = null;
      Statement st;
      if ((st = call as Statement) != null) {
        type = StatementRegister.GetStatementType(st);
      } else {
        ApplySuffix aps;
        if ((aps = call as ApplySuffix) != null) {
          type = StatementRegister.GetStatementType(aps);
          // convert applySuffix to an updateStmt
          st = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
        }
      }

      if (type != null) {
        Debug.WriteLine($"Resolving statement {type}");
        var resolverInstance = Activator.CreateInstance(type, this) as IAtomicLazyStmt;
        if (resolverInstance != null) {
          return resolverInstance.Resolve(st, solution);
        }
        Contract.Assert(false, Error.MkErr(st, 18, type.ToString(), typeof(IAtomicLazyStmt)));
      } else {

        Debug.WriteLine("Could not determine statement type");
        if (call is TacticVarDeclStmt) {
          Debug.WriteLine("Found tactic variable declaration");
          return RegisterLocalVariable(call as TacticVarDeclStmt);
        }
        UpdateStmt us;
        if ((us = st as UpdateStmt) != null) {
          if (StaticContext.program.IsTacticCall(us)) {
            Debug.WriteLine("Found nested tactic call");
            return ResolveNestedTacticCall(us);
          }
          if (IsLocalAssignment(us)) {
            Debug.WriteLine("Found local assignment");
            return UpdateLocalVariable(us);
          }
          if (IsArgumentApplication(us)) {
            Debug.WriteLine("Found argument applicaiton");
            return ResolveArgumentApplication(us);
          }
        } else if (call is PredicateStmt) {
          var predicate = call as PredicateStmt;
          return ResolvePredicateStmt(predicate);

        }
      }
      // insert the statement "as is"
      return CallDefaultAction(st);
    }

    private IEnumerable<Solution> ResolvePredicateStmt(PredicateStmt p) {
      foreach (var result in ResolveExpression(p.Expr)) {
        var resultExpression = result is IVariable ? VariableToExpression(result as IVariable) : result as Expression;
        Printer.P.GetConsolePrinter().PrintExpression(resultExpression, true);
        PredicateStmt newPredicate;
        if (p is AssertStmt) {
          newPredicate = new AssertStmt(p.Tok, p.EndTok, resultExpression, p.Attributes);
        } else {
          newPredicate = new AssumeStmt(p.Tok, p.EndTok, resultExpression, p.Attributes);
        }
        yield return AddNewStatement(p, newPredicate);
      }
    }

    private IEnumerable<Solution> ResolveArgumentApplication(UpdateStmt us) {
      Contract.Requires<ArgumentNullException>(us != null);
      var name = GetNameSegment(us);
      var key = GetLocalKeyByName(name);
      var dafnyFormal = key as Formal;
      // if the application is passed as an argument
      if (dafnyFormal != null) {
        var value = GetLocalValueByName(name);
        switch (key.Type.ToString()) {
          case "Tactic":
            var application = value as ApplySuffix;
            // this may cause problems when resolved tactic returns an ApplySuffix
            if (application != null) {

              Debug.WriteLine("Argument application is tactic");
              var newUpdateStmt = new UpdateStmt(us.Tok, us.EndTok, us.Lhss, new List<AssignmentRhs> { new ExprRhs(application) });
              var ac = Copy();
              foreach (var argument in application.Args) {
                var ns = argument as NameSegment;
                if (!StaticContext.HasGlobalVariable(ns?.Name)) continue;
                var temp = StaticContext.GetGlobalVariable(ns?.Name);
                ac.DynamicContext.AddLocal(new Formal(ns?.tok, ns?.Name, temp.Type, true, temp.IsGhost), ns);
              }
              // let's resolve the tactic applicaiton
              foreach (var item in ac.CallAction(newUpdateStmt, new Solution(Copy()))) {
                yield return item;
              }

            } else {
              if (value is LiteralExpr) {
                yield return new Solution(Copy());
              }
            }
            break;
          case "Term":
            // we only need to replace the term 
            // is term only an application????
            var term = value as NameSegment;
            if (term != null) {
              var oldAps = ((ExprRhs)us.Rhss[0]).Expr as ApplySuffix;
              var newAps = new ApplySuffix(oldAps?.tok, term, Util.Copy.CopyExpressionList(oldAps?.Args));
              yield return AddNewExpression(newAps);
            } else {
              if (value is Expression)
                yield return AddNewExpression(Util.Copy.CopyExpression(value as Expression));
            }
            break;
          default:
            Contract.Assert(false, Error.MkErr(us, 1, typeof(NameSegment)));
            break;
        }
        /* other types go here */
      } else {
        var value = GetLocalValueByName(name);
        var aps = ((ExprRhs)us.Rhss[0]).Expr as ApplySuffix;
        var member = value as MemberDecl;
        if (member == null) yield break;
        var newNs = new NameSegment(name.tok, member.Name, null);
        var expressionList = new List<Expression>();
        if (aps?.Args != null)
          foreach (var arg in aps.Args) {
            foreach (var result in ResolveExpression(arg)) {
              if (result is Expression)
                expressionList.Add(result as Expression);
              else if (result is IVariable)
                expressionList.Add(VariableToExpression(result as IVariable));
              else {
                Contract.Assert(false, "Sum tin wong");
                break; // we assume that the the call returns only one expression
              }
            }
          }
        aps = new ApplySuffix(aps?.tok, newNs, expressionList);
        Printer.P.GetConsolePrinter().PrintExpression(aps, true);
        var newUs = new UpdateStmt(us.Tok, us.EndTok, us.Lhss, new List<AssignmentRhs> { new ExprRhs(aps) });
        yield return AddNewStatement(us, newUs);
      }
    }

    public IEnumerable<object> ResolveExpression(Expression argument) {
      Contract.Requires<ArgumentNullException>(argument != null);
      NameSegment ns;
      ApplySuffix aps;
      if ((ns = argument as NameSegment) != null) {
        if (HasLocalWithName(ns))
          yield return GetLocalValueByName(ns.Name) ?? ns;
        else yield return ns;
      } else if ((aps = argument as ApplySuffix) != null) {

        var type = StatementRegister.GetAtomicType(aps);
        // if apply suffix is an atomic
        if (type != StatementRegister.Atomic.Undefined || IsArgumentApplication(aps)) {
          var us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
          // create a unique local variable
          var lv = new LocalVariable(aps.tok, aps.tok, aps.Lhs.tok.val, new BoolType(), false);
          var tvds = new TacticVarDeclStmt(us.Tok, us.EndTok, new List<LocalVariable> { lv }, us);
          foreach (var item in CallAction(tvds, new Solution(Copy()))) {
            var res = item.State.DynamicContext.localDeclarations[lv];
            DynamicContext.localDeclarations.Remove(lv);
            yield return res;
          }
        } else {
          if (StaticContext.program.IsTacticCall(aps)) {
            // rewrite the application
            var argsList = new List<Expression>();
            foreach (var arg in aps.Args) {
              foreach (var result in ResolveExpression(arg)) {
                if (result is Expression)
                  argsList.Add(result as Expression);
                else
                  argsList.Add(VariableToExpression(result as IVariable));
                break;
              }
            }
            yield return new ApplySuffix(aps.tok, aps.Lhs, argsList);

          } else if (aps.Lhs is ExprDotName) {
            foreach (var solution in ResolveExpression(aps.Lhs)) {
              yield return new ApplySuffix(aps.tok, solution as Expression, aps.Args);
            }
          } else {
            var argsList = new List<Expression>();
            foreach (var arg in aps.Args) {
              foreach (var result in ResolveExpression(arg)) {
                if (result is Expression)
                  argsList.Add(result as Expression);
                else
                  argsList.Add(VariableToExpression(result as IVariable));
                break;
              }
              yield return new ApplySuffix(aps.tok, aps.Lhs, argsList);
            }
          }
        }
      } else if (argument is TacnyBinaryExpr) {
        var newAps = new ApplySuffix(CreateToken("TacnyBinaryExpr", 0, 0), argument, new List<Expression>()); // create a wrapper for the TacnyBinaryExpr
        foreach (var result in CallAction(newAps, new Solution(Copy()))) {
          yield return result.State.DynamicContext.generatedExpressions[0]; // we assume that BinaryExpr only generates one expression
        }
      } else if (argument is BinaryExpr || argument is ParensExpression || argument is QuantifierExpr) {
        var expt = ExpressionTree.ExpressionToTree(argument);
        if (IsResolvable(expt)) {
          ResolveExpression(ref expt);
          yield return EvaluateExpression(expt);
        } else {
          foreach (var result in ResolveExpressionTree(expt)) {
            yield return result.TreeToExpression();
          }
        }
      } else {
        var name = argument as ExprDotName;
        if (name != null) {
          var edn = name;
          if (HasLocalWithName(edn.Lhs as NameSegment)) {
            ns = edn.Lhs as NameSegment;
            var newLhs = GetLocalValueByName(ns?.Name) ?? ns;
            yield return new ExprDotName(edn.tok, newLhs as Expression, edn.SuffixName, edn.OptTypeArguments);
          } else {
            yield return edn;
          }
        } else {
          var op = argument as UnaryOpExpr;
          if (op != null) {
            var unaryOp = op;
            foreach (var result in ResolveExpression(unaryOp.E)) {
              switch (unaryOp.Op) {
                case UnaryOpExpr.Opcode.Cardinality:
                  if (!(result is IEnumerable)) {
                    var resultExp = result is IVariable ? VariableToExpression(result as IVariable) : result as Expression;
                    yield return new UnaryOpExpr(unaryOp.tok, unaryOp.Op, resultExp);
                  } else {
                    var @enum = result as IList;
                    if (@enum != null) yield return new LiteralExpr(unaryOp.tok, new BigInteger(@enum.Count));
                  }
                  yield break;
                case UnaryOpExpr.Opcode.Not:
                  if (result is LiteralExpr) {
                    var lit = result as LiteralExpr;
                    if (lit.Value is bool) {
                      // inverse the bool value
                      yield return new LiteralExpr(unaryOp.tok, !(bool)lit.Value);
                    } else {
                      Contract.Assert(false, Error.MkErr(op, 1, "boolean"));
                    }
                  } else {
                    var resultExp = result is IVariable ? VariableToExpression(result as IVariable) : result as Expression;
                    yield return new UnaryOpExpr(unaryOp.tok, unaryOp.Op, resultExp);
                  }
                  yield break;
                default:
                  Contract.Assert(false, "Unsupported Unary Operator");
                  yield break;
              }
            }
          } else {
            var expression = argument as DisplayExpression;
            if (expression != null) {
              if (expression.Elements.Count == 0) {
                yield return Util.Copy.CopyExpression(expression);
              } else {
                foreach (var item in ResolveDisplayExpression(expression)) {
                  yield return item;
                }
              }
            } else { yield return argument; }
          }
        }
      }
    }


    private IEnumerable<IList<Expression>> ResolveDisplayExpression(DisplayExpression list) {
      Contract.Requires(list != null);
      var dict = new Dictionary<Expression, IEnumerable<object>>();
      foreach (var element in list.Elements) {
        dict.Add(element, ResolveExpression(element));
      }

      return GenerateList(null, dict);
    }


    private static IEnumerable<IList<Expression>> GenerateList(IList<Expression> list, Dictionary<Expression, IEnumerable<object>> elements) {
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
          var resultExpr = result is IVariable ? VariableToExpression(result as IVariable) : result as Expression;
          tmp.Add(resultExpr);
          foreach (var value in GenerateList(tmp, elements)) {
            yield return value;
          }
        }
      }
    }

    private IEnumerable<Solution> RegisterLocalVariable(TacticVarDeclStmt declaration) {
      Contract.Requires(declaration != null);
      // if declaration has rhs
      if (declaration.Update != null) {
        var rhs = declaration.Update as UpdateStmt;
        // if statement is of type var q;
        if (rhs == null) {
          /* leave the statement as is  */
        } else {
          foreach (var item in rhs.Rhss) {
            var index = rhs.Rhss.IndexOf(item);
            Contract.Assert(declaration.Locals.ElementAtOrDefault(index) != null, Error.MkErr(declaration, 8));
            var exprRhs = item as ExprRhs;
            // if the declaration is literal expr (e.g. tvar q := 1)
            var litExpr = exprRhs?.Expr as LiteralExpr;
            if (litExpr != null) {
              yield return AddNewLocal(declaration.Locals[index], litExpr);
            } else if (exprRhs?.Expr is ApplySuffix) {
              var aps = (ApplySuffix)exprRhs.Expr;
              foreach (var result in CallAction(aps, new Solution(Copy()))) {
                // we don't know where the results are, thus let's look at both statements and expressions
                result.State.Fin();
                var resultExpressions = result.State.DynamicContext.generatedExpressions;
                var resultStatements = result.State.GetResolved();
                if (resultStatements.Count > 0)
                  yield return AddNewLocal(declaration.Locals[index], resultStatements[0]); // we only expect a function to return a single expression
                else if (resultExpressions.Count > 0)
                  yield return AddNewLocal(declaration.Locals[index], resultExpressions[0]); // we only expect a function to return a single expression
                else
                  yield return AddNewLocal<object>(declaration.Locals[index], null);
              }
            } else {
              yield return AddNewLocal(declaration.Locals[index], exprRhs?.Expr);
            }
          }
        }
      } else {
        foreach (var item in declaration.Locals)
          AddLocal(item, null);
        yield return new Solution(Copy());

      }
    }

    private IEnumerable<Solution> ResolveNestedTacticCall(UpdateStmt us) {
      Contract.Requires<ArgumentNullException>(us != null);

      var tactic = StaticContext.program.GetTactic(us);
      var er = us.Rhss[0] as ExprRhs;
      foreach (var result in ResolveNestedTacticCall(tactic, er?.Expr as ApplySuffix)) {

        if (tactic is Tactic) {
          yield return AddNewStatement(result.State.GetResult());
        } else if (tactic is TacticFunction) {
          foreach (var value in result.State.DynamicContext.generatedExpressions) {
            yield return AddNewExpression(value);
            break;
          }
        }
      }
    }

    private IEnumerable<Solution> ResolveNestedTacticCall(ITactic tactic, ApplySuffix aps) {
      Contract.Requires<ArgumentNullException>(tactic != null && aps != null);
      var us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs> { new ExprRhs(aps) });
      var atomic = new Atomic(DynamicContext.md, tactic, us, StaticContext) {
        DynamicContext =
        {
          generatedExpressions = DynamicContext.generatedExpressions,
          generatedStatements = DynamicContext.generatedStatements,
          whileStmt = DynamicContext.whileStmt
        }
      };
      // transfer while stmt info
      //// register local variables
      Contract.Assert(aps.Args.Count == atomic.DynamicContext.tactic.Ins.Count);
      atomic.SetNewTarget(GetNewTarget());
      ResolveTacticArguments(atomic);

      return ResolveTactic(atomic, false);
    }

    private void ResolveTacticArguments(Atomic atomic) {
      var aps = ((ExprRhs)atomic.GetTacticCall().Rhss[0]).Expr as ApplySuffix;
      var exps = aps?.Args;
      Contract.Assert(exps.Count == atomic.DynamicContext.tactic.Ins.Count);
      for (var i = 0; i < exps?.Count; i++) {
        foreach (var result in ResolveExpression(exps[i])) {
          atomic.AddLocal(atomic.DynamicContext.tactic.Ins[i], result);
        }
      }
    }
    private IEnumerable<Solution> UpdateLocalVariable(UpdateStmt updateStmt) {
      Contract.Requires(updateStmt != null);
      // if statement is of type var q;
      Contract.Assert(updateStmt.Lhss.Count == updateStmt.Rhss.Count, Error.MkErr(updateStmt, 8));
      foreach (var var in updateStmt.Lhss) {
        var variable = var as NameSegment;
        Contract.Assert(variable != null, Error.MkErr(updateStmt, 5, typeof(NameSegment), var.GetType()));
        Contract.Assert(HasLocalWithName(variable), Error.MkErr(updateStmt, 9, variable.Name));
        // get the key of the variable
        var local = GetLocalKeyByName(variable);
        foreach (var item in updateStmt.Rhss) {
          // unfold the rhs
          var exprRhs = item as ExprRhs;
          if (exprRhs == null) continue;
          // if the expression is a literal value update the value
          var litVal = exprRhs.Expr as LiteralExpr;
          if (litVal != null) {
            AddLocal(local, litVal);
            yield return new Solution(Copy());
          } else { // otherwise process the expression
            foreach (var result in ResolveExpression(exprRhs.Expr)) {
              AddLocal(local, result);
              yield return new Solution(Copy());
            }
          }
        }
      }
    }

    /// <summary>
    /// Clear local variables, and fill them with tactic arguments. Use with caution.
    /// </summary>
    public void FillTacticInputs() {
      DynamicContext.FillTacticInputs();
    }

    protected void InitArgs(Statement st, out List<Expression> callArguments) {
      Contract.Requires(st != null);
      Contract.Ensures(Contract.ValueAtReturn(out callArguments) != null);
      IVariable lv;
      InitArgs(st, out lv, out callArguments);
    }

    /// <summary>
    /// Extract statement arguments and local variable definition
    /// </summary>
    /// <param name="st">Atomic statement</param>
    /// <param name="lv">Local variable</param>
    /// <param name="callArguments">List of arguments</param>
    /// <returns>Error message</returns>
    protected void InitArgs(Statement st, out IVariable lv, out List<Expression> callArguments) {
      Contract.Requires(st != null);
      Contract.Ensures(Contract.ValueAtReturn(out callArguments) != null);
      lv = null;
      callArguments = null;
      TacticVarDeclStmt tvds;
      UpdateStmt us;
      TacnyBlockStmt tbs;
      // tacny variables should be declared as tvar or tactic var
      if (st is VarDeclStmt)
        Contract.Assert(false, Error.MkErr(st, 13));

      if ((tvds = st as TacticVarDeclStmt) != null) {
        lv = tvds.Locals[0];
        callArguments = GetCallArguments(tvds.Update as UpdateStmt);

      } else if ((us = st as UpdateStmt) != null) {
        if (us.Lhss.Count == 0)
          callArguments = GetCallArguments(us);
        else {
          var ns = (NameSegment)us.Lhss[0];
          if (HasLocalWithName(ns)) {
            lv = GetLocalKeyByName(ns);
            callArguments = GetCallArguments(us);
          } else
            Printer.Error(st, "Local variable {0} is not declared", ns.Name);
        }
      } else if ((tbs = st as TacnyBlockStmt) != null) {
        var pe = tbs.Guard as ParensExpression;
        callArguments = pe != null ? new List<Expression> { pe.E } : new List<Expression> { tbs.Guard };
      } else
        Printer.Error(st, "Wrong number of method result arguments; Expected {0} got {1}", 1, 0);

    }

    private IEnumerable<Solution> CallDefaultAction(Statement st) {
      Contract.Requires(st != null);
      var state = Copy();
      state.AddUpdated(st, st);
      yield return new Solution(state);
    }

    #region getters
    protected static List<Expression> GetCallArguments(UpdateStmt us) {
      Contract.Requires(us != null);
      var er = (ExprRhs)us.Rhss[0];
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
      var tok = new Token(line, col) { val = val };
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
    public void SetNewTarget(MemberDecl newTarget) {
      DynamicContext.newTarget = newTarget;
    }


    /// <summary>
    /// Replaces the statement at current body counter with a new statement
    /// </summary>
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
        return EvaluateLeaf(expt) as LiteralExpr;
      }
      var bexp = tcce.NonNull(expt.Data as BinaryExpr);
      if (BinaryExpr.IsEqualityOp(bexp.Op)) {
        bool boolVal = EvaluateEqualityExpression(expt);
        return new LiteralExpr(new Token(), boolVal);
      }
      var lhs = EvaluateExpression(expt.LChild) as LiteralExpr;
      var rhs = EvaluateExpression(expt.RChild) as LiteralExpr;
      // for now asume lhs and rhs are integers
      var l = (BigInteger)lhs?.Value;
      var r = (BigInteger)rhs?.Value;

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

      return new LiteralExpr(lhs.tok, res);
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
        var lit = EvaluateLeaf(expt) as LiteralExpr;
        return lit?.Value is bool && (bool)lit.Value;
      }
      // left branch only
      if (expt.LChild != null && expt.RChild == null)
        return EvaluateEqualityExpression(expt.LChild);
      // if there is no more nesting resolve the expression
      if (expt.LChild.IsLeaf() && expt.RChild.IsLeaf()) {
        LiteralExpr lhs = null;
        LiteralExpr rhs = null;
        lhs = EvaluateLeaf(expt.LChild) as LiteralExpr;
        rhs = EvaluateLeaf(expt.RChild) as LiteralExpr;
        if (lhs?.GetType() == rhs?.GetType())
          return false;
        var bexp = tcce.NonNull(expt.Data as BinaryExpr);
        int res = -1;
        if (lhs?.Value is BigInteger) {
          var l = (BigInteger)lhs.Value;
          var r = (BigInteger)rhs?.Value;
          res = l.CompareTo(r);
        } else if (lhs?.Value is string) {
          var l = (string)lhs.Value;
          var r = rhs?.Value as string;
          res = string.Compare(l, r, StringComparison.Ordinal);
        } else if (lhs?.Value is bool) {
          res = ((bool)lhs.Value).CompareTo(rhs?.Value != null && (bool)rhs?.Value);
        }

        switch (bexp.Op) {
          case BinaryExpr.Opcode.Eq:
            return res == 0;
          case BinaryExpr.Opcode.Neq:
            return res != 0;
          case BinaryExpr.Opcode.Ge:
            return res >= 0;
          case BinaryExpr.Opcode.Gt:
            return res > 0;
          case BinaryExpr.Opcode.Le:
            return res <= 0;
          case BinaryExpr.Opcode.Lt:
            return res < 0;
        }
      } else { // evaluate a nested expression

        var bexp = tcce.NonNull(expt.Data as BinaryExpr);
        switch (bexp.Op) {
          case BinaryExpr.Opcode.And:
            return EvaluateEqualityExpression(expt.LChild) && EvaluateEqualityExpression(expt.RChild);
          case BinaryExpr.Opcode.Or:
            return EvaluateEqualityExpression(expt.LChild) || EvaluateEqualityExpression(expt.RChild);
        }
      }
      return false;
    }

    protected IEnumerable<ExpressionTree> ResolveTacticFunction(ExpressionTree body) {
      Contract.Requires(body != null);

      var leafResolvers = new Dictionary<ExpressionTree, IEnumerable<object>>
      {
        {body, ResolveExpression(body.TreeToExpression())}
      };


      return GenerateExpressionTree(body, leafResolvers);
    }



    protected IEnumerable<ExpressionTree> ResolveExpressionTree(ExpressionTree expression) {
      var leafs = expression.GetLeafs();
      var leafResolvers = new Dictionary<ExpressionTree, IEnumerable<object>>();
      foreach (var leaf in leafs)
        leafResolvers.Add(leaf, ResolveExpression(leaf.TreeToExpression()));

      return GenerateExpressionTree(expression, leafResolvers);
    }

    /// <summary>
    /// Resolve Tacny level expressions
    /// </summary>
    /// <param></param>
    /// <param name="tree"></param>
    /// <param name="leafResolvers"></param>
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
          newValue = VariableToExpression(value as IVariable);
        else
          Contract.Assert(false, "Sum tin wong");
        var newLeaf = ExpressionTree.ExpressionToTree(newValue);
        var newTree = ExpressionTree.FindAndReplaceNode(tree, newLeaf, kvp.Key);

        foreach (var result in GenerateExpressionTree(newTree, leafResolvers))
          yield return result;
      }
    }

    /// <summary>
    /// Evaluate a leaf node
    /// TODO: support for call evaluation
    /// </summary>
    /// <param name="expt"></param>
    /// <returns></returns>
    protected object EvaluateLeaf(ExpressionTree expt) {
      Contract.Requires(expt != null && expt.IsLeaf());
      if (expt.Data is LiteralExpr)
        return expt.Data;
      return ResolveExpression(expt.Data).FirstOrDefault();
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
        var result = EvaluateLeaf(guard);

        // we only need to replace nameSegments
        if (!(guard.Data is NameSegment)) return;
        Contract.Assert(result != null);
        Expression newNs; // potential encapsulation problems
        if (result is MemberDecl) {
          MemberDecl md = result as MemberDecl;
          newNs = new StringLiteralExpr(new Token(), md.Name, true);
        } else if (result is Formal) {
          var tmp = result as Formal;
          newNs = new NameSegment(tmp.tok, tmp.Name, null);
        } else if (result is NameSegment) {
          newNs = result as NameSegment;
        } else {
          newNs = result as Expression;// Dafny.LiteralExpr;
        }
        guard.Data = newNs;
      } else {
        ResolveExpression(ref guard.LChild);
        if (guard.RChild != null)
          ResolveExpression(ref guard.RChild);
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
      Contract.Requires(expt.IsRoot());
      List<Expression> leafs = expt.GetLeafData();

      foreach (var leaf in leafs) {
        if (leaf is NameSegment) {
          NameSegment ns = leaf as NameSegment;
          if (HasLocalWithName(ns)) {
            var local = GetLocalValueByName(ns);
            if (local is ApplySuffix || local is IVariable || local is NameSegment || local is Statement)
              return false;
          } else {
            return false;
          }
        } else if (!(leaf is LiteralExpr)) {
          if (leaf is ApplySuffix) {
            return false;
            //if (StaticContext.program.IsTacticCall(leaf as ApplySuffix) || IsArgumentApplication(leaf as ApplySuffix)) {
            //  return false;
            //}
          }
        }
      }
      return true;
    }

    public Solution AddNewStatement<T>(T oldValue, T newValue) where T : Statement {
      var ac = Copy();
      // THIS MIGHT CAUSE BUGS as previously generated stmts will not be carried over!!!!!
      //ac.DynamicContext.generatedStatements = new Dictionary<Statement, Statement>();
      // !!!!!!!!!!!!!!!!!!!!!!!!!!
      ac.AddUpdated(oldValue, newValue);
      return new Solution(ac);
    }

    public Solution AddNewStatement<T>(Dictionary<T, T> dict) where T : Statement {
      var ac = Copy();
      foreach (var kvp in dict) {
        ac.AddUpdated(kvp.Key, kvp.Value);
      }
      return new Solution(ac);
    }

    public Solution AddNewExpression<T>(T newValue) where T : Expression {
      var ac = Copy();
      ac.DynamicContext.generatedExpressions.Add(newValue);
      return new Solution(ac);
    }
    public Solution AddNewLocal<T>(IVariable variable, T value) where T : class {
      var ac = Copy();
      ac.AddLocal(variable, value);
      return new Solution(ac);
    }

    /// <summary>
    /// Generate a Dafny program and verify it
    /// </summary>
    public bool ResolveAndVerify(Solution solution) {
      Contract.Requires<ArgumentNullException>(solution != null);

      Microsoft.Dafny.Program prog = StaticContext.program.ParseProgram();
      solution.GenerateProgram(ref prog);
      StaticContext.program.ClearBody(DynamicContext.md);

      StaticContext.program.PrintMember(prog, solution.State.StaticContext.md.Name);

      if (!StaticContext.program.ResolveProgram())
        return false;
      StaticContext.program.VerifyProgram();
      return true;

    }



    /// <summary>
    /// Register datatype ctor vars as locals and set active ctor in the context
    /// </summary>
    /// <param name="datatype"></param>
    /// <param name="index"></param>
    public void RegisterLocals(DatatypeDecl datatype, int index, Dictionary<string, Type> ctorTypes = null) {
      Contract.Requires(datatype != null);
      Contract.Requires(index + 1 <= datatype.Ctors.Count);
      datatype.Ctors[index].EnclosingDatatype = datatype;
      DynamicContext.activeCtor = datatype.Ctors[index];
      foreach (var formal in datatype.Ctors[index].Formals) {
        // register globals as name segments
        // registed the ctor argument with the correct type
        if (ctorTypes != null) {
          var udt = formal.Type as UserDefinedType;
          if (udt != null) {
            if (ctorTypes.ContainsKey(udt.Name)) {
              var newFormal = new Formal(formal.Tok, formal.Name, ctorTypes[udt.Name], formal.InParam, formal.IsGhost);
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
      DynamicContext.activeCtor = null;
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
      return IsArgumentApplication(nameSegment);
    }

    [Pure]
    protected bool IsArgumentApplication(ApplySuffix aps) {
      Contract.Requires(aps != null);
      var nameSegment = GetNameSegment(aps);

      return nameSegment != null && IsArgumentApplication(nameSegment);
    }

    [Pure]
    protected bool IsArgumentApplication(NameSegment ns) {
      Contract.Requires(ns != null);
      return DynamicContext.HasLocalWithName(ns);
    }

    [Pure]
    public static Expression VariableToExpression(IVariable variable) {
      Contract.Requires(variable != null);
      return new NameSegment(variable.Tok, variable.Name, null);
    }

    [Pure]
    protected static NameSegment GetNameSegment(UpdateStmt us) {
      Contract.Requires(us != null);
      var rhs = us.Rhss[0] as ExprRhs;
      return rhs == null ? null : GetNameSegment(rhs.Expr as ApplySuffix);
    }

    [Pure]
    protected static NameSegment GetNameSegment(ApplySuffix aps) {
      var lhs = aps.Lhs as ExprDotName;
      if (lhs == null) return aps?.Lhs as NameSegment;
      var edn = lhs;
      return edn.Lhs as NameSegment;
    }

    [Pure]
    protected LocalVariable GenerateFreshLocalVariable(NameSegment ns) {
      int num = DynamicContext.localDeclarations.Count(i => i.Key.Name == ns.Name);
      return new LocalVariable(ns.tok, ns.tok, $"{ns.Name}_{num}", new ObjectType(), true);
    }
    [Pure]
    protected string GetArgumentType(NameSegment ns) {
      Contract.Requires(ns != null);
      var original = GetLocalKeyByName(ns);
      if (original == null)
        return null;
      try {
        return original.Type.ToString();
      } catch {
        // argument is LocalVariable, thus we don't have a type name
        return null;
      }
    }

    [Pure]
    protected BinaryExpr.Opcode StringToOp(string op) {
      foreach (BinaryExpr.Opcode code in Enum.GetValues(typeof(BinaryExpr.Opcode))) {
        try {
          if (BinaryExpr.OpcodeString(code) == op)
            return code;
        } catch (cce.UnreachableException) {
          throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
        }
      }
      throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
    }


    [Pure]
    protected static bool SingletonEquality(Expression a, Expression b) {
      var expr = a as LiteralExpr;
      if (expr != null) {
        var litA = expr;
        var litB = b as LiteralExpr;
        return litA.Value == litB?.Value;
      }
      var nsA = GetNameSegment(a);
      var nsB = GetNameSegment(b);
      if (!(a is ApplySuffix) && !(b is ApplySuffix)) return nsA?.Name.Equals(nsB?.Name) ?? false;
      var apsA = a as ApplySuffix;
      var apsB = b as ApplySuffix;
      // check if any of the args match
      if (apsA != null) {
        if (apsA.Args.Any(arg => SingletonEquality(arg, nsB))) {
          return true;
        }
      } else {
        foreach (var arg in apsB.Args) {
          if (SingletonEquality(arg, nsA)) {
            return true;
          }
        }
      }
      return nsA?.Name.Equals(nsB?.Name) ?? false;
    }


    [Pure]
    protected static NameSegment GetNameSegment(Expression a) {
      NameSegment ns = null;
      var segment = a as NameSegment;
      if (segment != null)
        ns = segment;
      else if (a is ExprDotName) {
        ns = ((ExprDotName) a).Lhs as NameSegment;
      } else if (a is SeqSelectExpr) {
        ns = ((SeqSelectExpr) a).E0 as NameSegment;
      }

      return ns;
    }

    [Pure]
    protected bool ValidateType(IVariable variable, BinaryExpr expression) {
      if (expression == null) {
        return true;
      }
      var type = StaticContext.GetVariableType(VariableToExpression(variable) as NameSegment);
      if (type == null)
        return true;

      switch (expression.Op) {
        case BinaryExpr.Opcode.Iff:
        case BinaryExpr.Opcode.Imp:
        case BinaryExpr.Opcode.Exp:
        case BinaryExpr.Opcode.And:
        case BinaryExpr.Opcode.Or:
          return type is BoolType;
        case BinaryExpr.Opcode.Eq:
        case BinaryExpr.Opcode.Neq:
        case BinaryExpr.Opcode.Lt:
        case BinaryExpr.Opcode.Le:
        case BinaryExpr.Opcode.Ge:
        case BinaryExpr.Opcode.Gt:
          if (type is CharType)
            return true;
          if (!(type is IntType || type is RealType))
            return false;
          goto case BinaryExpr.Opcode.Add;
        case BinaryExpr.Opcode.Add:
        case BinaryExpr.Opcode.Sub:
        case BinaryExpr.Opcode.Mul:
        case BinaryExpr.Opcode.Div:
        case BinaryExpr.Opcode.Mod:
          return type is IntType || type is RealType;
        case BinaryExpr.Opcode.Disjoint:
        case BinaryExpr.Opcode.In:
        case BinaryExpr.Opcode.NotIn:
          return type is CollectionType;
        default:
          Contract.Assert(false, "Unsupported Binary Operator");
          return false;
      }
    }
  }
}

