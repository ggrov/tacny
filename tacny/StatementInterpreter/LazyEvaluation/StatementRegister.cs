using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Type = System.Type;

namespace LazyTacny {
  
  class StatementRegister {
    public enum Atomic {
      Undefined = 0,
      Invar,
      ReplaceSingleton,
      ExtractGuard,
      ReplaceOp,
      IsValid,
      AddMatch,
      Perm,
      Or,
      Id,
      Fail,
      AddVariant,
      Variables,
      Params,
      SuchThat,
      Lemmas,
      MergeLists,
      While,
      If,
      Solved,
      Changed,
      Functions,
      PreCond,
      PostCond,
      IsInductive,
      TryCatch,
      Returns,
      IsDatatype,
      GetMember,
      FreshLemName,
      GenBexp,
      TacnyBexp,
      GetCtor,
      Delete,
      IfGuard,
      Split,
      Consts,
      ReplaceConst,
      Subst,
      Predicates
    }

    public static Dictionary<string, Atomic> AtomicSignature = new Dictionary<string, Atomic>
    {
      {"replace_singleton", Atomic.ReplaceSingleton},
      {"loop_guard", Atomic.ExtractGuard},
      {"replace_operator", Atomic.ReplaceOp},
      {"is_valid", Atomic.IsValid},
      {"cases", Atomic.AddMatch},
      {"explore", Atomic.Perm},
      {"||", Atomic.Or},
      {"id", Atomic.Id},
      {"fail", Atomic.Fail},
      {"add_variant", Atomic.AddVariant},
      {"variables", Atomic.Variables},
      {"params", Atomic.Params},
      {":|", Atomic.SuchThat},
      {"lemmas", Atomic.Lemmas},
      {"merge", Atomic.MergeLists},
      {"solved", Atomic.Solved},
      {"changed", Atomic.Changed},
      {"functions", Atomic.Functions},
      {"preconditions", Atomic.PreCond},
      {"postconditions", Atomic.PostCond},
      {"is_inductive", Atomic.IsInductive},
      {"tryCatch", Atomic.TryCatch},
      {"get_returns", Atomic.Returns},
      {"is_datatype", Atomic.IsDatatype},
      {"caller", Atomic.GetMember },
      {"fresh_lem_name", Atomic.FreshLemName },
      {"gen_bexp", Atomic.GenBexp },
      {"|||", Atomic.TacnyBexp },
      {"get_constructor", Atomic.GetCtor },
      {"delete", Atomic.Delete },
      { "if_guard", Atomic.IfGuard },
      {"split", Atomic.Split },
      { "consts", Atomic.Consts },
      { "replace_constants", Atomic.ReplaceConst },
      { "subst", Atomic.Subst },
      { "predicates", Atomic.Predicates }
    };

    public static Dictionary<Atomic, Type> AtomicClass = new Dictionary<Atomic, Type>
    {
      //{Atomic.REPLACE_SINGLETON, typeof(SingletonAtomic)},
      {Atomic.Invar, typeof(InvariantAtomic)},
      {Atomic.AddMatch, typeof(MatchAtomic)},
      {Atomic.ReplaceOp, typeof(OperatorAtomic)},
      {Atomic.ExtractGuard, typeof(GuardAtomic)},
      {Atomic.Perm, typeof(PermAtomic)},
      //{Atomic.OR, typeof(OrAtomic)},
      {Atomic.Id, typeof(IdAtomic)},
      {Atomic.Fail, typeof(FailAtomic)},
      //{Atomic.ADD_VARIANT, typeof(VariantAtomic)},
      {Atomic.Variables, typeof(VariablesAtomic)},
      {Atomic.Params, typeof(ParamsAtomic)},
      {Atomic.MergeLists, typeof(MergeAtomic)},
      {Atomic.SuchThat, typeof(SuchThatAtomic)},
      {Atomic.Lemmas, typeof(LemmasAtomic)},
      {Atomic.If, typeof(IfAtomic)},
      {Atomic.While, typeof(WhileAtomic)},
      //{Atomic.SOLVED, typeof(SolvedAtomic)},
      //{Atomic.CHANGED, typeof(ChangedAtomic)},
      //{Atomic.FUNCTIONS, typeof(FunctionsAtomic)},
      {Atomic.PreCond, typeof(PrecondAtomic)},
      {Atomic.PostCond, typeof(PostcondAtomic)},
      {Atomic.GetMember, typeof(GetMemberAtomic) },
      {Atomic.IsInductive, typeof(IsInductiveAtomic)},
      {Atomic.TryCatch, typeof(TryCatchAtomic)},
      {Atomic.Returns, typeof(ReturnAtomic)},
      //{Atomic.IS_DATATYPE, typeof(IsDatatypeAtomic)},
      {Atomic.FreshLemName, typeof(FreshNameAtomic) },
      {Atomic.GenBexp, typeof(GenBexpAtomic) },
      {Atomic.TacnyBexp, typeof(ExpressionAtomic) },
      {Atomic.GetCtor, typeof(GetConstructorAtomic) },
      {Atomic.Delete, typeof(DeleteAtomic) },
      {Atomic.IfGuard, typeof(IfGuardAtomic) },
      {Atomic.Split, typeof(SplitAtomic) },
      {Atomic.Consts, typeof(ConstantsAtomic) },
      {Atomic.ReplaceConst, typeof(ReplaceConstantAtomic) },
      {Atomic.Subst, typeof(OperatorAtomic) },
      {Atomic.Predicates, typeof(PredicateAtomic) }
  };

    /// <summary>
    /// Return the Atomic type of the statement
    /// </summary>
    /// <param name="call"></param>
    /// <returns>statement atomic type</returns>
    public static Atomic GetAtomicType(object call) {
      Contract.Requires(call != null);
      Statement st;
      ApplySuffix aps;
      if ((st = call as Statement) != null)
        return GetAtomicType(st);
      if ((aps = call as ApplySuffix) != null)
        return GetAtomicType(aps);

      return Atomic.Undefined;
    }


    public static Atomic GetAtomicType(Statement st) {
      UpdateStmt us = null;
      TacnyBlockStmt tbs;
      TacticVarDeclStmt tvds;
      VarDeclStmt vds;
      OrStmt os;
      IToken tok = null;
      if ((tbs = st as TacnyBlockStmt) != null) {
        TacnyCasesBlockStmt tcbs;
        TacnySolvedBlockStmt tsbs;
        TacnyChangedBlockStmt tchbs;
        TacnyTryCatchBlockStmt ttcbs;
        if ((tcbs = tbs as TacnyCasesBlockStmt) != null)
          return AtomicSignature[tcbs.WhatKind];
        if ((tsbs = tbs as TacnySolvedBlockStmt) != null)
          return AtomicSignature[tsbs.WhatKind];
        if ((tchbs = tbs as TacnyChangedBlockStmt) != null)
          return AtomicSignature[tchbs.WhatKind];
        if ((ttcbs = tbs as TacnyTryCatchBlockStmt) != null)
          return AtomicSignature[ttcbs.WhatKind];
      } else if (((os = st as OrStmt) != null)) {
        tok = os.Tok;
      } else if (st is IfStmt)
        return Atomic.If;
      else if (st is WhileStmt)
        return Atomic.While;
      else if ((us = st as UpdateStmt) != null) { } else if ((vds = st as VarDeclStmt) != null) {
        AssignSuchThatStmt suchThat = vds.Update as AssignSuchThatStmt;
        // check if declaration is such that
        if (suchThat != null)
          tok = suchThat.Tok;
        else
          us = vds.Update as UpdateStmt;
      } else if ((tvds = st as TacticVarDeclStmt) != null) {
        var suchThat = tvds.Update as AssignSuchThatStmt;
        // check if declaration is such that
        if (suchThat != null)
          tok = suchThat.Tok;
        else
          us = tvds.Update as UpdateStmt;
      } else if (st is TacticInvariantStmt) {
        return Atomic.Invar;
      }

      if (tok != null)
        return GetAtomicType(tok);

      if (us == null)
        return Atomic.Undefined;

      var er = (ExprRhs)us.Rhss[0];
      return GetAtomicType(er.Expr as ApplySuffix);
    }
    public static Atomic GetAtomicType(ApplySuffix aps) {
      if (aps == null)
        return Atomic.Undefined;
      return GetAtomicType(aps.Lhs.tok);
    }

    /// <summary>
    /// Return atomic type from string signature
    /// </summary>
    /// <returns>Atomic type</returns>
    public static Atomic GetAtomicType(IToken tok) {
      Contract.Requires<ArgumentNullException>(tok != null);
      string name = tok.val;
      if (!AtomicSignature.ContainsKey(name))
        return Atomic.Undefined;

      return AtomicSignature[name];
    }


    public static Type GetStatementType(Statement st) {
      Contract.Requires(st != null);
      return GetStatementType(GetAtomicType(st));
    }

    public static Type GetStatementType(ApplySuffix aps) {
      Contract.Requires(aps != null);
      return GetStatementType(GetAtomicType(aps));
    }

    public static Type GetStatementType(Atomic atomic) {
      if (!AtomicClass.ContainsKey(atomic))
        return null;
      return AtomicClass[atomic];
    }

  }
}
