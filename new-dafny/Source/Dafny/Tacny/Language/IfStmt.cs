using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;
using dfy = Microsoft.Dafny;
using System.Diagnostics.Contracts;
/*
namespace Tacny.Language {
  public class IfStmt : FlowControlStmt {
    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {

      var guard = ExtractGuard(statement);

      Contract.Assert(guard != null, "guard");
      return IsResolvable(guard, state) ? EvaluateIf(statement as dfy.IfStmt, guard, state) :
        InsertIf(statement as dfy.IfStmt, guard, state);
    }

    /// <summary>
    /// Evaluate tacny level if statement
    /// </summary>
    /// <param name="ifStmt"></param>
    /// <param name="guard"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    private IEnumerable<ProofState> EvaluateIf(dfy.IfStmt ifStmt, ExpressionTree guard, ProofState state) {
      if (ExpressionTree.EvaluateEqualityExpression(guard, state)) {
        return Interpreter.EvaluateBlockStmt(state, ifStmt.Thn);
      }
      if (ifStmt.Els == null) return null;
      var els = ifStmt.Els as BlockStmt;
      return els != null ? Interpreter.EvaluateBlockStmt(state, els) :
        EvaluateIf(ifStmt.Els as dfy.IfStmt, ExtractGuard(ifStmt.Els), state);
    }

    /// <summary>
    /// Insert the if statement into dafny code
    /// </summary>
    /// <param name="ifStmt"></param>
    /// <param name="guard"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    private IEnumerable<ProofState> InsertIf(dfy.IfStmt ifStmt, ExpressionTree guard, ProofState state) {
      // resolve the if statement guard
      foreach (var newGuard in ExpressionTree.ResolveExpression(guard, state)) {
        var expr = newGuard.TreeToExpression();
        var p = new Printer(Console.Out);
        p.PrintExpression(expr, false);
        newGuard.SetParent();
        var resultExpression = newGuard.TreeToExpression();
        // resolve 'if' block
        var ifStmtEnum = Interpreter.EvaluateBlockStmt(state, ifStmt.Thn);
        IEnumerable<ProofState> elseStmtEnum = null;
        if (ifStmt.Els != null) {
          // resovle else block
          var stmt = ifStmt.Els as BlockStmt;
          elseStmtEnum = stmt != null ? Interpreter.EvaluateBlockStmt(state, ifStmt.Thn) :
            InsertIf(ifStmt.Els as dfy.IfStmt, ExtractGuard(ifStmt.Els), state);
        }
        foreach (var statement in GenerateIfStmt(ifStmt, resultExpression, ifStmtEnum, elseStmtEnum, state)) {
          yield return statement;
        }
      }
    }

    private IEnumerable<ProofState> GenerateIfStmt(dfy.IfStmt original, Expression guard,
      IEnumerable<ProofState> ifStmtEnum, IEnumerable<ProofState> elseStmtEnum,
      ProofState state) {
      var cl = new Cloner();

      foreach (var @if in ifStmtEnum) {
        var statementList = @if.GetGeneratedCode();
        var ifBody = new BlockStmt(original.Thn.Tok, original.Thn.EndTok, statementList);
        if (elseStmtEnum != null) {
          foreach (var @else in elseStmtEnum) {
            var elseList = @else.GetGeneratedCode();
            Statement elseBody = null;
            // if original body was a plain else block
            if (original.Els is BlockStmt)
              elseBody = new BlockStmt(original.Els.Tok, original.Thn.EndTok, elseList);
            else // otherwise it was a 'else if' and the solution list should only contain one if stmt
              elseBody = elseList[0];
            var c = state.Copy();

            c.AddStatement(new dfy.IfStmt(original.Tok, original.EndTok, original.IsExistentialGuard,
              cl.CloneExpr(guard), ifBody, elseBody));
            yield return c;
          }
        } else {
          var c = state.Copy();
          c.AddStatement(new dfy.IfStmt(original.Tok, original.EndTok, original.IsExistentialGuard, cl.CloneExpr(guard),
            ifBody, null));
          yield return c;
        }
      }
    }
  }
}
*/