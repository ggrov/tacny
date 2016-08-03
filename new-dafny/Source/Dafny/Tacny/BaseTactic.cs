using System.Diagnostics.Contracts;

namespace Tacny {
  [ContractClass(typeof(BaseTacticContract))]
  public abstract class BaseTactic {
    // Atom signature
   public abstract string Signature { get; }
    public abstract int ArgsCount { get; }
  }

  [ContractClassFor(typeof(BaseTactic))]
  public abstract class BaseTacticContract : BaseTactic {
    public override string Signature { get; }

    [ContractInvariantMethod]
    private void ObjectInvariant() {
      Contract.Invariant(Signature != null);
    }
  }
}