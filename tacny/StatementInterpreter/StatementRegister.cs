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
        };


        public static Atomic GetAtomicType(Statement st)
        {
            Contract.Requires(st != null);
            ExprRhs er;
            UpdateStmt us;
            TacnyBlockStmt tbs;
            VarDeclStmt vds;
            string name;
            if ((tbs = st as TacnyBlockStmt) != null)
            {
                TacnyCasesBlockStmt tcbs;
                TacnyCasesBlockStmt tibs;
                if((tcbs = tbs as TacnyCasesBlockStmt) != null)
                    return atomic_signature[tcbs.WhatKind];
                else if ((tibs = tbs as TacnyCasesBlockStmt) != null)
                    return atomic_signature[tibs.WhatKind];
            }
            if (st is IfStmt || st is WhileStmt)
                return Atomic.COMPOSITION;
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
