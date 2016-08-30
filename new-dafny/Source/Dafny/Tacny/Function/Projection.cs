using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;


/* Projection Functions for */
namespace Tacny.Function {
  abstract class Projection : BaseTactic {
    //public override string Signature // keywords to trigger a call to the actual implementation 
    //public override int ArgsCount  // number of the args, in future we need types as well which needed by resover
    public abstract IEnumerable<object> Generate(Expression expression, ProofState proofState);
  }
}