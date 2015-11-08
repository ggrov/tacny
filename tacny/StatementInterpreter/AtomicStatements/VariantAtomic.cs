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
            List<Expression> dec_list = null;
            Expression input = null;
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
                    input = new WildcardExpr(wildCard.tok);

                }
            }
            else
            {
                // hack
                /*
                 * TODO:
                 * Implement propper variable replacement
                 */
                object tmp;
                err = ProcessArg(call_arguments[0], out tmp);

                IVariable form = tmp as IVariable;
                if (form != null)
                    input = new NameSegment(form.Tok, form.Name, null);
                else if (tmp is BinaryExpr)
                {
                    BinaryExpr bexp = tmp as BinaryExpr;
                    ProcessArg(bexp.E0, out tmp);
                    form = tmp as IVariable;
                    NameSegment e0 = new NameSegment(form.Tok, form.Name, null);
                    ProcessArg(bexp.E1, out tmp);
                    form = tmp as IVariable;
                    NameSegment e1 = new NameSegment(form.Tok, form.Name, null);

                    input = new BinaryExpr(bexp.tok, bexp.Op, e0, e1);
                }
                else if (tmp is NameSegment)
                {
                    input = tmp as NameSegment;
                }
            }
            WhileStmt ws = FindWhileStmt(globalContext.tac_call, globalContext.md);

            if (ws != null)
            {
                WhileStmt nws = null;
                dec_list = new List<Expression>(ws.Decreases.Expressions.ToArray());

                dec_list.Add(input);
                Specification<Expression> decreases = new Specification<Expression>(dec_list, ws.Attributes);
                nws = new WhileStmt(ws.Tok, ws.EndTok, ws.Guard, ws.Invariants, decreases, ws.Mod, ws.Body);
                IncTotalBranchCount();
                AddUpdated(ws, nws);

            }
            else
            {


                Method target = Program.FindMember(program.ParseProgram(), localContext.md.Name) as Method;

                if (target == null)
                    return FormatError("add_variant", "Could not find target method");

                dec_list = target.Decreases.Expressions;
                // insert new variants at the end of the existing variants list
                Contract.Assert(input != null);
                dec_list.Add(input);

                Specification<Expression> decreases = new Specification<Expression>(dec_list, target.Decreases.Attributes);
                Method result = null;
                if (target is Lemma)
                {
                    Lemma oldLm = target as Lemma;
                    result = new Lemma(oldLm.tok, oldLm.Name, oldLm.HasStaticKeyword, oldLm.TypeArgs, oldLm.Ins, oldLm.Outs,
                        oldLm.Req, oldLm.Mod, oldLm.Ens, decreases, oldLm.Body, oldLm.Attributes, oldLm.SignatureEllipsis);
                }
                else if (target is CoLemma)
                {
                    CoLemma oldCl = target as CoLemma;
                    result = new CoLemma(oldCl.tok, oldCl.Name, oldCl.HasStaticKeyword, oldCl.TypeArgs, oldCl.Ins, oldCl.Outs,
                        oldCl.Req, oldCl.Mod, oldCl.Ens, decreases, oldCl.Body, oldCl.Attributes, oldCl.SignatureEllipsis);

                }
                else
                {
                    result = new Method(target.tok, target.Name, target.HasStaticKeyword, target.IsGhost, target.TypeArgs,
                        target.Ins, target.Outs, target.Req, target.Mod, target.Ens, decreases, target.Body, target.Attributes,
                        target.SignatureEllipsis);
                }
                // register new method
                this.localContext.new_target = result;
            }

            IncTotalBranchCount();
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }


    }
}
