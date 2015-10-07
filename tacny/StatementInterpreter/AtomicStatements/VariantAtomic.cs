using System;
using Microsoft.Dafny;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;

namespace Tacny
{
    class VariantAtomic : Atomic, IAtomicStmt
    {
        public VariantAtomic(Atomic atomic) : base(atomic) { }

        public string FormatError(string method, string error)
        {
            return "ERROR " + method + ": " + error;
        }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return AddVariant(st, ref solution_list);
        }


        public string AddVariant(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> call_arguments = null;
            UpdateStmt us;
            string err;
            if (st is UpdateStmt)
                us = st as UpdateStmt;
            else
                return FormatError("add_variant", "does not have a return value");

            err = InitArgs(st, out call_arguments);
            if (err != null)
                return FormatError("add_variant", err);

            if (call_arguments.Count != 1)
                return FormatError("add_variant", "Unexpected number of arguments, expected 1 received " + call_arguments.Count);
            StringLiteralExpr wildCard = call_arguments[0] as StringLiteralExpr;
            if (wildCard != null)
            {
                if (wildCard.Value.Equals("*"))
                {
                    call_arguments[0] = new WildcardExpr(wildCard.tok);
                }
            }

            Method target = Program.FindMember(program.dafnyProgram, localContext.md.Name) as Method;
            if (target == null)
                return FormatError("add_variant", "Could not find target method");

            List<Expression> dec_list = target.Decreases.Expressions;
            // insert new variants at the end of the existing variants list
            dec_list = dec_list.Concat(call_arguments).ToList();

            Specification<Expression> decreases = new Specification<Expression>(dec_list, target.Decreases.Attributes);

            Method result = new Method(target.tok, target.Name, target.HasStaticKeyword, target.IsGhost, target.TypeArgs,
                target.Ins, target.Outs, target.Req, target.Mod, target.Ens, decreases, target.Body, target.Attributes, target.SignatureEllipsis);

            // register new method
            this.localContext.new_target = result;
            IncTotalBranchCount();
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }


    }
}
