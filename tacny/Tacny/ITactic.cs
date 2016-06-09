using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;

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
