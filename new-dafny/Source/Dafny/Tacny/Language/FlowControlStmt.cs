﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;
using dfy = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;
using Tacny.Atomic;
using Tacny.Language;

namespace Tacny.Language {
  [ContractClass(typeof(FlowControlStmtContract))]
  public abstract class FlowControlStmt : BaseTactic {
    public override string Signature { get; }
    public override int ArgsCount { get; }

    public abstract IEnumerable<ProofState> Generate(Statement statement, ProofState state);
  

    protected static bool IsResolvable(ExpressionTree guard, ProofState state) {
      Contract.Requires<ArgumentNullException>(guard != null, "guard");

      return ExpressionTree.IsResolvable(guard, state);
    }

    protected static ExpressionTree ExtractGuard(Statement stmt) {
      Contract.Requires<ArgumentNullException>(stmt != null);
      // extract the guard statement
      var ifStmt = stmt as dfy.IfStmt;
      if (ifStmt != null) {
        return ExpressionTree.ExpressionToTree(ifStmt.Guard);
      }
      var whileStmt = stmt as WhileStmt;
      return whileStmt != null ? ExpressionTree.ExpressionToTree(whileStmt.Guard) : null;
    }
  }
}

[ContractClassFor(typeof(FlowControlStmt))]
public class FlowControlStmtContract : FlowControlStmt {
  public override string Signature { get; }
  public override int ArgsCount { get; }

  public override IEnumerable<ProofState> Generate(Statement statement, ProofState state) {
    Contract.Requires(statement != null);
    Contract.Requires(state != null);

    yield break;
  }
}
