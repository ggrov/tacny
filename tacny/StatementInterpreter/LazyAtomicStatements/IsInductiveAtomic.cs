using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace LazyTacny {
  class IsInductiveAtomic : Atomic, IAtomicLazyStmt {
    public IsInductiveAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      List<Expression> call_arguments = null;
      IVariable lv = null;
      DatatypeDecl datatype = null;

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(tcce.OfSize(call_arguments, 1), Error.MkErr(st, 0, 1, call_arguments.Count));

      string argType = GetArgumentType(call_arguments[0] as NameSegment);

      foreach (var result in ResolveExpression(call_arguments[0])) {
        if (argType == "Element") {
          var ns = result as NameSegment;
          IVariable originalDecl = StaticContext.GetGlobalVariable(ns.Name);
          Contract.Assert(originalDecl != null, Error.MkErr(st, 9, ((NameSegment)call_arguments[0]).Name));
          var datatypeName = originalDecl.Type.ToString();
          datatype = StaticContext.GetGlobal(datatypeName);
          var lit = IsInductive(datatypeName, datatype);
          yield return AddNewLocal(lv, lit);
        } else {
          var ctor = result as DatatypeCtor;
          Contract.Assert(ctor != null, Error.MkErr(st, 1, "Datatype constructor"));
          var datatypeName = ctor.EnclosingDatatype.Name;

          LiteralExpr lit = new LiteralExpr(st.Tok, false);

          foreach (var formal in ctor.Formals) {
            if (formal.Type.ToString() == datatypeName) {
              lit = new LiteralExpr(st.Tok, true);
              break;
            }
          }
          yield return AddNewLocal(lv, lit);
        }
      }
    }


    private static LiteralExpr IsInductive(string datatypeName, DatatypeDecl datatype) {
      LiteralExpr lit = new LiteralExpr(datatype.tok, false);
      foreach (var ctor in datatype.Ctors) {
        foreach (var formal in ctor.Formals) {
          if (formal.Type.ToString() == datatypeName) {
            return new LiteralExpr(datatype.tok, true);
          }
        }
      }

      return lit;
    }

  }
}
