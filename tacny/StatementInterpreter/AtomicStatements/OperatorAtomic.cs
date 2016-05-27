using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class OperatorAtomic : Atomic, IAtomicStmt
    {
        public OperatorAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            ReplaceOperator(st, ref solution_list);
        }
        
        public void ReplaceOperator(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            StringLiteralExpr old_operator = null;
            StringLiteralExpr new_operator = null;
            Expression formula = null;
            BinaryExpr.Opcode old_op;
            BinaryExpr.Opcode new_op;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 3), Error.MkErr(st, 0, 3, call_arguments.Count));

            old_operator = (StringLiteralExpr)call_arguments[0];
            new_operator = (StringLiteralExpr)call_arguments[1];
            ProcessArg(call_arguments[2], out formula);
            Contract.Assert(formula != null, Error.MkErr(st, 10));
            
            old_op = ToOpCode((string)old_operator.Value);
            new_op = ToOpCode((string)new_operator.Value);
            
            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);
            List<Expression> exp_list = new List<Expression>();

            ReplaceOp(old_op, new_op, et, ref exp_list);

            if (exp_list.Count == 0)
                exp_list.Add(formula);

           
            // smells like unnecessary branching if no replacement happened.
            for (int i = 0; i < exp_list.Count; i++)
            {
                AddLocal(lv, exp_list[i]);     
                solution_list.Add(new Solution(Copy()));
            }
        }

        protected Expression ReplaceOp(BinaryExpr.Opcode old_op, BinaryExpr.Opcode new_op, ExpressionTree formula, ref List<Expression> nexp)
        {
            Contract.Requires(nexp != null);
            if (formula == null)
                return null;

            if (formula.Data is BinaryExpr)
            {
                if (((BinaryExpr)formula.Data).Op == old_op)
                {
                    ExpressionTree nt = formula.Copy();
                    nt.Data = new BinaryExpr(formula.Data.tok, new_op, ((BinaryExpr)formula.Data).E0, ((BinaryExpr)formula.Data).E1);
                    nexp.Add(nt.Root.TreeToExpression());
                    return null;
                }
            }
            ReplaceOp(old_op, new_op, formula.LChild, ref nexp);
            ReplaceOp(old_op, new_op, formula.RChild, ref nexp);
            return null;
        }

        protected BinaryExpr.Opcode ToOpCode(string op)
        {
            foreach (BinaryExpr.Opcode code in Enum.GetValues(typeof(BinaryExpr.Opcode)))
            {
                try
                {
                    if (BinaryExpr.OpcodeString(code) == op)
                        return code;
                }
                catch (cce.UnreachableException)
                {
                    throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
                }

            }
            throw new ArgumentException("Invalid argument; Expected binary operator, received " + op);
        }
    }
}
