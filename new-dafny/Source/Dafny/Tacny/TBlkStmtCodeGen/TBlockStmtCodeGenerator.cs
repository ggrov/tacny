using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace Tacny.TBlkStmtCodeGen {
  abstract class TBlockStmtCodeGenerator{
    public abstract string WhatKind { get; }
    public abstract List<Statement> Generate(List<Statement> stmts);
  }

  class DefaultTBlockStmtCodeGen : TBlockStmtCodeGenerator{
    public override string WhatKind => "default";
    public override List<Statement> Generate(List<Statement> stmts){
      return stmts;
    }
  }
}
