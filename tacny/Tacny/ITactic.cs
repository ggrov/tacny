using System.Diagnostics.Contracts;

namespace Tacny {
  [ContractClass(typeof(ITacticContract))]
  public interface ITactic {
    // Atom signature
    string Signature { get; }
   
  }

  [ContractClassFor(typeof(ITactic))]
  public abstract class ITacticContract : ITactic {

    public string Signature { get; }

    [ContractInvariantMethod]
    private void ObjectInvariant() {
      Contract.Invariant(Signature != null);
    }
  }
}
