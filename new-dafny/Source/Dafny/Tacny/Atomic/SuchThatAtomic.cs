using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace Tacny.Atomic {
  class SuchThatAtomic : Atomic {
    public override string Signature => ":|";
    public override int ArgsCount => -1;
    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {
      var tvds = statement as TacticVarDeclStmt;
      AssignSuchThatStmt suchThat = null;
      if (tvds != null)
        suchThat = tvds.Update as AssignSuchThatStmt;
      else if (statement is AssignSuchThatStmt) {
        suchThat = (AssignSuchThatStmt)statement;
      } else {
        Contract.Assert(false, "Unexpected statement type");
      }
      Contract.Assert(suchThat != null, "Unexpected statement type");

      BinaryExpr bexp = suchThat.Expr as BinaryExpr;
      var locals = new List<string>();
      if (tvds == null) {
        foreach (var item in suchThat.Lhss) {
          if (item is IdentifierExpr) {
            var id = (IdentifierExpr)item;
            if (state.ContainTacnyVal(id.Name))
              locals.Add(id.Name);
            else {
              //TODO: error
            }
          }
        }
      }
      else {
        locals = new List<string>(tvds.Locals.Select(x => x.Name).ToList());
      }
      // this will cause issues when multiple variables are used
      // as the variables are updated one at a time
      foreach (var local in locals) {
        foreach (var item in ResolveExpression(state, bexp, local)) {
          var copy = state.Copy();
          copy.UpdateTacnyVar(local, item);
          yield return copy;
        }
      }
    }

    private IEnumerable<object> ResolveExpression(ProofState state, Expression expr, string declaration) {
      Contract.Requires(expr != null);

      if (expr is BinaryExpr) {

        BinaryExpr bexp = (BinaryExpr) expr;
        switch (bexp.Op) {
          case BinaryExpr.Opcode.In:
           // Contract.Assert(var != null, Error.MkErr(bexp, 6, declaration.Name));
          //  Contract.Assert(var.Name == declaration.Name, Error.MkErr(bexp, 6, var.Name));
            foreach (var result in Interpreter.EvalTacnyExpression(state, bexp.E1)) {
              if (result is IEnumerable) {
                foreach (var item in (IEnumerable)result) {
                  yield return item;
                }
              }
            }
            yield break;
          case BinaryExpr.Opcode.And:
            // for each item in the resolved lhs of the expression
            foreach (var item in ResolveExpression(state, bexp.E0, declaration)) {
              var copy = state.Copy();
              copy.AddTacnyVar(declaration, item);
              // resolve the rhs expression
              foreach (var res in Interpreter.EvalTacnyExpression(copy, bexp.E1)) {
                LiteralExpr lit = res as LiteralExpr;
                // sanity check
                Contract.Assert(lit != null);
                if (lit.Value is bool) {
                  // if resolved to true
                  if ((bool)lit.Value) {
                    yield return item;
                  }
                }
              }
            }
            yield break;
        }
      }
    }
  }
}

