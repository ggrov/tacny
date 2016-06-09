using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;


namespace Tacny.EAtomic {
  /// <summary>
  /// Abstact class for Atomic Expressions
  /// </summary>
  [ContractClass(typeof(EAtomicContract))]
  public abstract class EAtomic : ITactic {
   public abstract string Signature { get; }
    
    /// <summary>
    /// Common entry point for each atomic
    /// </summary>
    /// <param name="expression">Expression to be resolved</param>
    /// <param name="proofState">Current tactic ProofState</param>
    /// <returns>Lazily returns generated objects one at a time</returns>
    public abstract IEnumerable<object> Generate(Expression expression, ProofState proofState);
  }

  [ContractClassFor(typeof(EAtomic))]
  public class EAtomicContract : EAtomic {

    public override string Signature { get; }
    public override IEnumerable<object> Generate(Expression expression, ProofState proofState) {
      Contract.Requires(expression != null);
      Contract.Requires(proofState != null);

      yield break;
    }
  }
}
