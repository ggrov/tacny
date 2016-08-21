using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace Tacny.Language {
    public class IfStmt : FlowControlStmt {
      public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {
          throw new NotImplementedException();
      }
  }
}
