using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

// Project the all dafny variables in the current scope of the calling method/function

namespace Tacny.Function { 
  class Variables : Projection {

    public override string Signature => "variables";
    public override int ArgsCount => 0;

    public override IEnumerable<object> Generate(Expression expression, ProofState proofState) {
      var vars = proofState.DafnyVariables.Values.ToList();
      yield return vars.Select(x => x.Variable).ToList();
    }
  }
}
