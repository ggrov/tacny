﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class SuchThatAtomic : Atomic, IAtomicLazyStmt {
    public SuchThatAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {

      foreach (var item in SuchThat(st, solution)) {
        yield return item;
      }
    }

    private IEnumerable<Solution> SuchThat(Statement st, Solution solution) {
      TacticVarDeclStmt tvds = st as TacticVarDeclStmt;
      // statement must be object level, thus include as is
      if (tvds == null) {
        yield return AddNewStatement(st, st);
        yield break;
      }

      AssignSuchThatStmt suchThat = tvds.Update as AssignSuchThatStmt;
      Contract.Assert(suchThat != null, Error.MkErr(st, 5, typeof(AssignSuchThatStmt), tvds.Update.GetType()));

      BinaryExpr bexp = suchThat.Expr as BinaryExpr;
      Contract.Assert(bexp != null, Error.MkErr(st, 5, typeof(BinaryExpr), suchThat.Expr.GetType()));

      // this will cause issues when multiple variables are used
      // as the variables are updated one at a time
      foreach (var local in tvds.Locals) {
        foreach (var item in ResolveExpression(bexp, local)) {
          yield return AddNewLocal(local, item);
        }
      }
      
    }

    private IEnumerable<object> ResolveExpression(Expression expr, IVariable declaration) {
      Contract.Requires(expr != null);

      if (expr is BinaryExpr) {

        BinaryExpr bexp = expr as BinaryExpr;
        switch (bexp.Op) {
          case BinaryExpr.Opcode.In:
            NameSegment var = bexp.E0 as NameSegment;
            Contract.Assert(var != null, Error.MkErr(bexp, 6, declaration.Name));
            Contract.Assert(var.Name == declaration.Name, Error.MkErr(bexp, 6, var.Name));
            foreach (var result in ResolveExpression(bexp.E1)) {
              if (result is IEnumerable) {
                dynamic resultList = result;
                foreach (var item in resultList) {
                  yield return item;
                }
              }
            }
            yield break;
          case BinaryExpr.Opcode.And:
            // for each item in the resolved lhs of the expression
            foreach (var item in ResolveExpression(bexp.E0, declaration)) {
              Atomic copy = Copy();
              copy.AddLocal(declaration, item);
              // resolve the rhs expression
              foreach (var res in copy.ResolveExpression(bexp.E1)) {
                LiteralExpr lit = res as LiteralExpr;
                // sanity check
                Contract.Assert(lit != null, Error.MkErr(expr, 17));
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
