using System;
using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
namespace Tacny
{
    class OperatorAtomic : Atomic, IAtomicStmt
    {
        public string FormatError(string error)
        {
            return "ERROR replace_operator: " + error;
        }

        public OperatorAtomic(Atomic atomic) : base(atomic) { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return ReplaceOperator(st, ref solution_list);
        }
        
        public string ReplaceOperator(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments = null;
            StringLiteralExpr old_operator = null;
            StringLiteralExpr new_operator = null;
            Expression formula = null;
            BinaryExpr.Opcode old_op;
            BinaryExpr.Opcode new_op;

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 3), Util.Error.MkErr(st, 0, 3, call_arguments.Count));

            old_operator = (StringLiteralExpr)call_arguments[0];
            new_operator = (StringLiteralExpr)call_arguments[1];
            ProcessArg(call_arguments[2], out formula);
            Contract.Assert(formula != null, Util.Error.MkErr(st, 10));
            try
            {
                old_op = ToOpCode((string)old_operator.Value);
                new_op = ToOpCode((string)new_operator.Value);
            }
            catch (ArgumentException e)
            {
                return FormatError(e.Message);
            }

            ExpressionTree et = ExpressionTree.ExpressionToTree(formula);
            List<Expression> exp_list = new List<Expression>();

            ReplaceOp(old_op, new_op, et, ref exp_list);

            if (exp_list.Count == 0)
                exp_list.Add(formula);

           
            // smells like unnecessary branching if no replacement happened.
            for (int i = 0; i < exp_list.Count; i++)
            {
                IncTotalBranchCount();
                AddLocal(lv, exp_list[i]);     
                solution_list.Add(new Solution(this.Copy()));
            }

            return null;
        }

        protected Expression ReplaceOp(BinaryExpr.Opcode old_op, BinaryExpr.Opcode new_op, ExpressionTree formula, ref List<Expression> nexp)
        {
            Contract.Requires(nexp != null);
            if (formula == null)
                return null;

            if (formula.data is BinaryExpr)
            {
                if (((BinaryExpr)formula.data).Op == old_op)
                {
                    ExpressionTree nt = formula.Copy();
                    nt.data = new BinaryExpr(formula.data.tok, new_op, ((BinaryExpr)formula.data).E0, ((BinaryExpr)formula.data).E1);
                    nexp.Add(nt.root.TreeToExpression());
                    return null;
                }
            }
            ReplaceOp(old_op, new_op, formula.lChild, ref nexp);
            ReplaceOp(old_op, new_op, formula.rChild, ref nexp);
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
