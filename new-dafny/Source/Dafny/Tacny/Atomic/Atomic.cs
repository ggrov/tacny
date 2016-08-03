using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace Tacny.Atomic {
  /// <summary>
  ///   Abstract class for Atomic Statement
  /// </summary>
  [ContractClass(typeof(AtomicContract))]
  public abstract class Atomic : BaseTactic {
    public abstract override string Signature { get; }

    /// <summary>
    /// </summary>
    /// <param name="statement"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public abstract IEnumerable<ProofState> Generate(Statement statement, ProofState state);
  }

  [ContractClassFor(typeof(Atomic))]
  public class AtomicContract : Atomic {
    public override string Signature { get; }
    public override int ArgsCount { get; }

    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {
      Contract.Requires(statement != null);
      Contract.Requires(state != null);

      yield break;
    }
  }
}