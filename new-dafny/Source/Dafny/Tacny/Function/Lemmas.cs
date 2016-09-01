using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace Tacny.Function{
  class Lemmas : Projection{
    public override string Signature => "lemmas";
    public override int ArgsCount => 0;

    private static bool IsLemma(MemberDecl m){
      return (m is Lemma || m is FixpointLemma);
    }
    /// <summary>
    /// Return the objects of all the lemmas
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="proofState"></param>
    /// <returns></returns>
    public override IEnumerable<object> Generate(Expression expression, ProofState proofState){
      var ls = proofState.Members.Values.ToList().Where(IsLemma);
      yield return ls;
    }
  }
}