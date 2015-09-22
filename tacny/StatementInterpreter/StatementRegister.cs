using Microsoft.Dafny;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class StatementRegister
    {
        public enum Atomic
        {
            UNDEFINED = 0,
            ADD_INVAR,
            CREATE_INVAR,
            REPLACE_SINGLETON,
            EXTRACT_GUARD,
            REPLACE_OP,
            COMPOSITION,
            IS_VALID,
            ADD_MATCH,
            ADD_IF,
            TRY_ALL,
            OR,
            ID,
            FAIL,
            ADD_VARIANT,
            VARIABLES,
            PARAMS,
        };

        public static Dictionary<string, Atomic> atomic_signature = new Dictionary<string, Atomic>()
        {
            {"replace_singleton", Atomic.REPLACE_SINGLETON},
            {"create_invariant", Atomic.CREATE_INVAR},
            {"add_invariant", Atomic.ADD_INVAR},
            {"extract_guard", Atomic.EXTRACT_GUARD},
            {"replace_operator", Atomic.REPLACE_OP},
            {"is_valid", Atomic.IS_VALID},
            {"cases", Atomic.ADD_MATCH},
            {"addif", Atomic.ADD_IF},
            {"tryall", Atomic.TRY_ALL},
            {"||", Atomic.OR},
            {"id", Atomic.ID},
            {"fail", Atomic.FAIL},
            {"add_variant", Atomic.ADD_VARIANT},
            {"variables", Atomic.VARIABLES},
            {"params", Atomic.PARAMS}
        };
        
        public static Dictionary<Atomic, System.Type> atomic_class = new Dictionary<Atomic, System.Type>()
        {
            {Atomic.REPLACE_SINGLETON, typeof(SingletonAtomic)},
            {Atomic.CREATE_INVAR, typeof(InvariantAtomic)},
            {Atomic.ADD_INVAR, typeof(InvariantAtomic)},
            {Atomic.ADD_IF, typeof(IfAtomic)},
            {Atomic.ADD_MATCH, typeof(MatchAtomic)},
            {Atomic.REPLACE_OP, typeof(OperatorAtomic)},
            {Atomic.EXTRACT_GUARD, typeof(GuardAtomic)},
            {Atomic.TRY_ALL, typeof(TryAllAtomic)},
            {Atomic.OR, typeof(OrAtomic)},
            {Atomic.COMPOSITION, typeof(CompositionAtomic)},
            {Atomic.ID, typeof(IdAtomic)},
            {Atomic.FAIL, typeof(FailAtomic)},
            {Atomic.ADD_VARIANT, typeof(VariantAtomic)},
            {Atomic.VARIABLES, typeof(VariablesAtomic)},
            {Atomic.PARAMS, typeof(ParamsAtomic)}
        };

        /// <summary>
        /// Return the Atomic type of the statement
        /// </summary>
        /// <param name="st">Statement to analyse</param>
        /// <returns>statement atomic type</returns>
        public static Atomic GetAtomicType(Statement st)
        {
            Contract.Requires(st != null);
            ExprRhs er;
            UpdateStmt us = null;
            TacnyBlockStmt tbs;
            VarDeclStmt vds;
            OrStmt os;
            string name;
            if ((tbs = st as TacnyBlockStmt) != null)
            {
                TacnyCasesBlockStmt tcbs;
                TacnyIfBlockStmt tibs;
                if((tcbs = tbs as TacnyCasesBlockStmt) != null)
                    return atomic_signature[tcbs.WhatKind];
                else if ((tibs = tbs as TacnyIfBlockStmt) != null)
                    return atomic_signature[tibs.WhatKind];
            }
            else if (((os = st as OrStmt) != null))
            {
                return atomic_signature[os.Tok.val];
            }
            else if (st is IfStmt || st is WhileStmt)
            {
                return Atomic.COMPOSITION;
            }
            else if ((us = st as UpdateStmt) != null) { }
            else if ((vds = st as VarDeclStmt) != null)
                us = vds.Update as UpdateStmt;
            
            if (us == null)
                return Atomic.UNDEFINED;

            er = (ExprRhs)us.Rhss[0];

            ApplySuffix aps = er.Expr as ApplySuffix;
            if(aps == null)
                return Atomic.UNDEFINED;

            name = aps.Lhs.tok.val;
            if (!atomic_signature.ContainsKey(name))
                return Atomic.UNDEFINED;

            return atomic_signature[name];
        }
        
        /// <summary>
        /// Return atomic type from string signature
        /// </summary>
        /// <param name="name">string signature</param>
        /// <returns>Atomic type</returns>
        public static Atomic GetAtomicType(string name)
        {
            Contract.Requires(name != null);

            if (!atomic_signature.ContainsKey(name))
                return Atomic.UNDEFINED;

            return atomic_signature[name];
        }


        public static System.Type GetStatementType(Statement st)
        {
            Contract.Requires(st != null);
            return GetStatementType(GetAtomicType(st));
        }

        public static System.Type GetStatementType(Atomic atomic)
        {
            if (!atomic_class.ContainsKey(atomic))
                return null;
            return atomic_class[atomic];
        }

    }
}
