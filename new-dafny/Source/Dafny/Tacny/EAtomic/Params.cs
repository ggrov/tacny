using System.Collections.Generic;
using System.Linq;
using Microsoft.Dafny;

namespace Tacny.EAtomic {
  class Params : EAtomic {
    public override string Signature => "params";
    public override int ArgsCount => 0;

    // parameters can be checked by combine the type Formal and the InParam attribute
    private static bool IsParam(ProofState.VariableData var) {
      if(var.Variable is Microsoft.Dafny.Formal) {
        var v = var.Variable as Microsoft.Dafny.Formal;
        return v.InParam;
      } else
        return false;

    }

    /// <summary>
    ///  Project the parameters of the calling method/function
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="proofState"></param>
    /// <returns></returns>
    public override IEnumerable<object> Generate(Expression expression, ProofState proofState) {
      var vars = proofState.GetAllDafnyVars().Values.ToList().Where(IsParam);
      yield return vars.Select(x => x.Variable).ToList();
    }
  }
}
