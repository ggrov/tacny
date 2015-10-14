﻿using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System;

namespace Util
{
    /// <summary>
    /// A helper class for creating deep object copies
    /// </summary>
    public static class Copy
    {
        /// <summary>
        /// Creates a deep copy of a statement list
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<Statement> CopyStatementList(List<Statement> source)
        {
            List<Statement> ret = new List<Statement>();
            foreach (var item in source)
            {
                ret.Add(CopyStatement(item));
            }
            return ret;
        }

        /// <summary>
        /// Deep copy a statement
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        public static Statement CopyStatement(Statement item)
        {

            if (item is UpdateStmt)
                return CopyUpdateStmt(item as UpdateStmt);

            return item;
        }

        /// <summary>
        /// Deep copy updateStmt
        /// </summary>
        /// <param name="stmt"></param>
        /// <returns></returns>
        public static UpdateStmt CopyUpdateStmt(UpdateStmt stmt)
        {
            ExprRhs old_exp = stmt.Rhss[0] as ExprRhs;
            ApplySuffix old_aps = old_exp.Expr as ApplySuffix;

            ApplySuffix aps = new ApplySuffix(old_aps.tok, old_aps.Lhs, CopyExpressionList(old_aps.Args)); // args might require a deep copy
            return new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
        }

        /// <summary>
        /// Deep copy expression list
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<Expression> CopyExpressionList(List<Expression> source)
        {
            List<Expression> ret = new List<Expression>();

            foreach (var item in source)
            {
                ret.Add(CopyExpression(item));
            }

            return ret;
        }
        /// <summary>
        /// Deep copy an expression
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public static Expression CopyExpression(Expression exp)
        {
            if (exp is NameSegment)
                return CopyNameSegment(exp as NameSegment);
            return exp;
        }

        /// <summary>
        /// Deep copy nameSegment
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public static NameSegment CopyNameSegment(NameSegment old)
        {
            return new NameSegment(old.tok, old.Name, old.OptTypeArguments);
        }


        public static CoLemma CopyCoLemma(MemberDecl md)
        {
            CoLemma oldCl = md as CoLemma;
            if (oldCl == null)
                return null;
            return CopyCoLemma(oldCl);
        }

        public static CoLemma CopyCoLemma(CoLemma oldCl)
        {

            return new CoLemma(oldCl.tok, oldCl.Name, oldCl.HasStaticKeyword, oldCl.TypeArgs, oldCl.Ins, oldCl.Outs, oldCl.Req, oldCl.Mod,
                oldCl.Ens, oldCl.Decreases, oldCl.Body, oldCl.Attributes, oldCl.SignatureEllipsis);
        }

        public static Lemma CopyLemma(MemberDecl md)
        {
            Lemma oldLm = md as Lemma;
            if (oldLm == null)
                return null;

            return CopyLemma(oldLm);
        }

        public static Lemma CopyLemma(Lemma oldLm)
        {
            return new Lemma(oldLm.tok, oldLm.Name, oldLm.HasStaticKeyword, oldLm.TypeArgs, oldLm.Ins, oldLm.Outs, oldLm.Req, oldLm.Mod,
                oldLm.Ens, oldLm.Decreases, oldLm.Body, oldLm.Attributes, oldLm.SignatureEllipsis);
        }

        public static Method CopyMethod(MemberDecl md)
        {
            Method oldMd = md as Method;
            if (oldMd == null)
                return null;
            return CopyMethod(oldMd);
        }

        public static Method CopyMethod(Method oldMd)
        {
            return new Method(oldMd.tok, oldMd.Name, oldMd.HasStaticKeyword, oldMd.IsGhost, oldMd.TypeArgs,
                oldMd.Ins, oldMd.Outs, oldMd.Req, oldMd.Mod, oldMd.Ens, oldMd.Decreases, oldMd.Body, oldMd.Attributes,
                oldMd.SignatureEllipsis);
        }

        public static Tactic CopyTactic(MemberDecl md)
        {
            Tactic oldTac = md as Tactic;
            if (oldTac == null)
                return null;
            return CopyTactic(oldTac);
        }

        public static Tactic CopyTactic(Tactic oldTac)
        {
            return new Tactic(oldTac.tok, oldTac.Name, oldTac.HasStaticKeyword, oldTac.TypeArgs,
                oldTac.Ins, oldTac.Outs, oldTac.Req, oldTac.Mod, oldTac.Ens, oldTac.Decreases, oldTac.Body,
                oldTac.Attributes, oldTac.SignatureEllipsis);
        }

        public static Method CopyMember(MemberDecl md)
        {
            if (md == null)
                return null;
            System.Type type = md.GetType();
            if (type == typeof(Lemma))
                return CopyLemma(md);
            else if (type == typeof(CoLemma))
                return CopyCoLemma(md);
            else if (type == typeof(Tactic))
                return CopyTactic(md);
            else
                return CopyMethod(md);
        }

        //todo test whether oldDict.values require deep copy
        public static Dictionary<Statement, Statement> CopyStatementDict(Dictionary<Statement, Statement> oldDict)
        {
            List<Statement> us_keys = new List<Statement>(oldDict.Keys);
            List<Statement> us_values = Util.Copy.CopyStatementList(new List<Statement>(oldDict.Values));
            Dictionary<Statement, Statement> newDict = us_keys.ToDictionary(x => x, x => us_values[us_keys.IndexOf(x)]);
            return newDict;
        }
        
    }
}