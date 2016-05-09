﻿using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace LazyTacny {
    /**
     * TODO
     * clean up
     * 
     */
    class StatementRegister {
        public enum Atomic {
            UNDEFINED = 0,
            ADD_INVAR,
            CREATE_INVAR,
            REPLACE_SINGLETON,
            EXTRACT_GUARD,
            REPLACE_OP,
            IS_VALID,
            ADD_MATCH,
            PERM,
            OR,
            ID,
            FAIL,
            ADD_VARIANT,
            VARIABLES,
            PARAMS,
            SUCH_THAT,
            LEMMAS,
            MERGE_LISTS,
            WHILE,
            IF,
            SOLVED,
            CHANGED,
            FUNCTIONS,
            PRE_COND,
            POST_COND,
            IS_INDUCTIVE,
            TRY_CATCH,
            RETURNS,
            IS_DATATYPE,
            GET_MEMBER,
            FRESH_LEM_NAME,
            GEN_BEXP
        };

        public static Dictionary<string, Atomic> atomic_signature = new Dictionary<string, Atomic>()
        {
            {"replace_singleton", Atomic.REPLACE_SINGLETON},
            {"create_invariant", Atomic.CREATE_INVAR},
            {"add_invariant", Atomic.ADD_INVAR},
            {"loop_guard", Atomic.EXTRACT_GUARD},
            {"replace_operator", Atomic.REPLACE_OP},
            {"is_valid", Atomic.IS_VALID},
            {"cases", Atomic.ADD_MATCH},
            {"explore", Atomic.PERM},
            {"||", Atomic.OR},
            {"id", Atomic.ID},
            {"fail", Atomic.FAIL},
            {"add_variant", Atomic.ADD_VARIANT},
            {"variables", Atomic.VARIABLES},
            {"params", Atomic.PARAMS},
            {":|", Atomic.SUCH_THAT},
            {"lemmas", Atomic.LEMMAS},
            {"merge", Atomic.MERGE_LISTS},
            {"solved", Atomic.SOLVED},
            {"changed", Atomic.CHANGED},
            {"functions", Atomic.FUNCTIONS},
            {"preconditions", Atomic.PRE_COND},
            {"postconditions", Atomic.POST_COND},
            {"is_inductive", Atomic.IS_INDUCTIVE},
            {"tryCatch", Atomic.TRY_CATCH},
            {"get_returns", Atomic.RETURNS},
            {"is_datatype", Atomic.IS_DATATYPE},
            {"member", Atomic.GET_MEMBER },
            {"fresh_lem_name", Atomic.FRESH_LEM_NAME },
            {"gen_bexp", Atomic.GEN_BEXP }
        };

        public static Dictionary<Atomic, System.Type> atomic_class = new Dictionary<Atomic, System.Type>()
        {
            //{Atomic.REPLACE_SINGLETON, typeof(SingletonAtomic)},
            {Atomic.CREATE_INVAR, typeof(InvariantAtomic)},
            {Atomic.ADD_INVAR, typeof(InvariantAtomic)},
            {Atomic.ADD_MATCH, typeof(MatchAtomic)},
            //{Atomic.REPLACE_OP, typeof(OperatorAtomic)},
            {Atomic.EXTRACT_GUARD, typeof(GuardAtomic)},
            {Atomic.PERM, typeof(PermAtomic)},
            //{Atomic.OR, typeof(OrAtomic)},
            {Atomic.ID, typeof(IdAtomic)},
            {Atomic.FAIL, typeof(FailAtomic)},
            //{Atomic.ADD_VARIANT, typeof(VariantAtomic)},
            {Atomic.VARIABLES, typeof(VariablesAtomic)},
            {Atomic.PARAMS, typeof(ParamsAtomic)},
            {Atomic.MERGE_LISTS, typeof(MergeAtomic)},
            {Atomic.SUCH_THAT, typeof(SuchThatAtomic)},
            {Atomic.LEMMAS, typeof(LemmasAtomic)},
            {Atomic.IF, typeof(IfAtomic)},
            {Atomic.WHILE, typeof(WhileAtomic)},
            //{Atomic.SOLVED, typeof(SolvedAtomic)},
            //{Atomic.CHANGED, typeof(ChangedAtomic)},
            //{Atomic.FUNCTIONS, typeof(FunctionsAtomic)},
            {Atomic.PRE_COND, typeof(PrecondAtomic)},
            {Atomic.POST_COND, typeof(PostcondAtomic)},
            {Atomic.GET_MEMBER, typeof(GetMemberAtomic) },
            //{Atomic.IS_INDUCTIVE, typeof(IsInductiveAtomic)},
            {Atomic.TRY_CATCH, typeof(TryCatchAtomic)},
            //{Atomic.RETURNS, typeof(ReturnAtomic)},
            //{Atomic.IS_DATATYPE, typeof(IsDatatypeAtomic)},
            {Atomic.FRESH_LEM_NAME, typeof(FreshNameAtomic) },
            {Atomic.GEN_BEXP, typeof(GenBexpAtomic) },
        };

        /// <summary>
        /// Return the Atomic type of the statement
        /// </summary>
        /// <param name="st">Statement to analyse</param>
        /// <returns>statement atomic type</returns>
        public static Atomic GetAtomicType(object call) {
            Contract.Requires(call != null);
            Statement st;
            ApplySuffix aps;
            if ((st = call as Statement) != null)
                return GetAtomicType(st);
            else if ((aps = call as ApplySuffix) != null)
                return GetAtomicType(aps);

            return Atomic.UNDEFINED;
        }


        public static Atomic GetAtomicType(Statement st) {
            ExprRhs er;
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
                    return atomic_signature[tcbs.WhatKind];
                else if ((tsbs = tbs as TacnySolvedBlockStmt) != null)
                    return atomic_signature[tsbs.WhatKind];
                else if ((tchbs = tbs as TacnyChangedBlockStmt) != null)
                    return atomic_signature[tchbs.WhatKind];
                else if ((ttcbs = tbs as TacnyTryCatchBlockStmt) != null)
                    return atomic_signature[ttcbs.WhatKind];
            } else if (((os = st as OrStmt) != null)) {
                tok = os.Tok;
            } else if (st is IfStmt)
                return Atomic.IF;
            else if (st is WhileStmt)
                return Atomic.WHILE;
            else if ((us = st as UpdateStmt) != null) { } else if ((vds = st as VarDeclStmt) != null) {
                AssignSuchThatStmt suchThat = vds.Update as AssignSuchThatStmt;
                // check if declaration is such that
                if (suchThat != null)
                    tok = suchThat.Tok;
                else
                    us = vds.Update as UpdateStmt;
            } else if ((tvds = st as TacticVarDeclStmt) != null) {
                AssignSuchThatStmt suchThat = tvds.Update as AssignSuchThatStmt;
                // check if declaration is such that
                if (suchThat != null)
                    tok = suchThat.Tok;
                else
                    us = tvds.Update as UpdateStmt;
            }
            if (tok != null)
                return GetAtomicType(tok);

            if (us == null)
                return Atomic.UNDEFINED;

            er = (ExprRhs)us.Rhss[0];
            return GetAtomicType(er.Expr as ApplySuffix);
        }
        public static Atomic GetAtomicType(ApplySuffix aps) {
            if (aps == null)
                return Atomic.UNDEFINED;
            return GetAtomicType(aps.Lhs.tok);
        }

        /// <summary>
        /// Return atomic type from string signature
        /// </summary>
        /// <param name="name">string signature</param>
        /// <returns>Atomic type</returns>
        public static Atomic GetAtomicType(IToken tok) {
            Contract.Requires<ArgumentNullException>(tok != null);
            string name = tok.val;
            if (!atomic_signature.ContainsKey(name))
                return Atomic.UNDEFINED;

            return atomic_signature[name];
        }


        public static System.Type GetStatementType(Statement st) {
            Contract.Requires(st != null);
            return GetStatementType(GetAtomicType(st));
        }

        public static System.Type GetStatementType(ApplySuffix aps) {
            Contract.Requires(aps != null);
            return GetStatementType(GetAtomicType(aps));
        }

        public static System.Type GetStatementType(Atomic atomic) {
            if (!atomic_class.ContainsKey(atomic))
                return null;
            return atomic_class[atomic];
        }

    }
}
