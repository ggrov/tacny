using System.Collections.Generic;
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


    }
}
