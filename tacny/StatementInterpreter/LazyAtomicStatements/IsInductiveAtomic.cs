using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Tacny;

namespace LazyTacny {
  class IsInductiveAtomic : Atomic, IAtomicLazyStmt {
    public IsInductiveAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      List<Expression> call_arguments = null;
      IVariable lv = null;
      string datatype_name = null;
      DatatypeDecl datatype = null;

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

      NameSegment argument = call_arguments[0] as NameSegment;
      Contract.Assert(argument != null, Util.Error.MkErr(st, 1, typeof(NameSegment)));

      // get the formal tactic input to determine the type
      Formal tac_input = (Formal)GetLocalKeyByName(argument);
      Contract.Assert(tac_input != null, Util.Error.MkErr(st, 9, argument.Name));

      foreach (var result in ResolveExpression(call_arguments[0])) {

      }

      return null;
    }

    public void Resolve(Statement st, ref List<Solution> solution_list) {
      List<Expression> call_arguments = null;
      IVariable lv = null;
      string datatype_name = null;
      DatatypeDecl datatype = null;

      InitArgs(st, out lv, out call_arguments);
      Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

      NameSegment argument = call_arguments[0] as NameSegment;
      Contract.Assert(argument != null, Util.Error.MkErr(st, 1, typeof(NameSegment)));

      // get the formal tactic input to determine the type
      Formal tac_input = (Formal)GetLocalKeyByName(argument);
      Contract.Assert(tac_input != null, Util.Error.MkErr(st, 9, argument.Name));

      foreach (var result in ResolveExpression(call_arguments[0])) {

      }
      datatype_name = tac_input.Type.ToString();
      /**
       * TODO cleanup
       * if datatype is Element lookup the formal in global variable registry
       */
      if (datatype_name == "Element") {
        // get the original variable declaration
        object val = GetLocalValueByName(argument.Name);
        NameSegment decl = val as NameSegment;

        Contract.Assert(decl != null, Util.Error.MkErr(st, 9, argument.Name));

        IVariable original_decl = StaticContext.GetGlobalVariable(decl.Name);
        Contract.Assert(original_decl != null, Util.Error.MkErr(st, 9, tac_input.Name));

        datatype_name = original_decl.Type.ToString();
      } else
        Contract.Assert(false, Util.Error.MkErr(st, 1, "Element"));

      Contract.Assert(datatype_name != null);
      if (!StaticContext.ContainsGlobalKey(datatype_name))
        Contract.Assert(false, Util.Error.MkErr(st, 12, datatype_name));

      argument = GetLocalValueByName(tac_input) as NameSegment;

      datatype = StaticContext.GetGlobal(datatype_name);
      LiteralExpr lit = new LiteralExpr(st.Tok, false);
      foreach (var ctor in datatype.Ctors) {
        foreach (var formal in ctor.Formals) {
          if (formal.Type.ToString() == datatype_name) {
            lit = new LiteralExpr(st.Tok, true);
            break;
          }
        }
      }

      DynamicContext.AddLocal(lv, lit);
    }
  }
}
