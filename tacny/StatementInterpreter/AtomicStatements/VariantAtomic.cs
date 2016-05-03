using Microsoft.Dafny;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
namespace Tacny
{
    class VariantAtomic : Atomic, IAtomicStmt
    {
        public VariantAtomic(Atomic atomic) : base(atomic) { }

        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            AddVariant(st, ref solution_list);
        }

        private void AddVariant(Statement st, ref List<Solution> solution_list)
        {
            List<Expression> call_arguments = null;
            List<Expression> dec_list = null;
            Expression input = null;

            InitArgs(st, out call_arguments);
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));

            StringLiteralExpr wildCard = call_arguments[0] as StringLiteralExpr;
            if (wildCard != null)
            {
                if (wildCard.Value.Equals("*"))
                    input = new WildcardExpr(wildCard.tok);
            }
            else
            {
                // hack
                /*
                 * TODO:
                 * Implement propper variable replacement
                 */
                object tmp;
                ProcessArg(call_arguments[0], out tmp);
                Contract.Assert(tmp != null);
                IVariable form = tmp as IVariable;
                if (form != null)
                    input = new NameSegment(form.Tok, form.Name, null);
                else if (tmp is BinaryExpr)
                {
                    input = tmp as BinaryExpr;
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
                AddUpdated(ws, nws);

            }
            else
            {
                Method target = Program.FindMember(globalContext.program.ParseProgram(), localContext.md.Name) as Method;
                if (GetNewTarget() != null && GetNewTarget().Name == target.Name)
                    target = GetNewTarget();
                Contract.Assert(target != null, Util.Error.MkErr(st, 3));
                
                dec_list = target.Decreases.Expressions;
                // insert new variants at the end of the existing variants list
                Contract.Assert(input != null);
                dec_list.Add(input);

                Specification<Expression> decreases = new Specification<Expression>(dec_list, target.Decreases.Attributes);
                Method result = null;
                dynamic lemma = null;
                if ((lemma = target as Lemma) != null)
                {
                    result = new Lemma(lemma.tok, lemma.Name, lemma.HasStaticKeyword, lemma.TypeArgs, lemma.Ins, lemma.Outs,
                        lemma.Req, lemma.Mod, lemma.Ens, decreases, lemma.Body, lemma.Attributes, lemma.SignatureEllipsis);
                }
                else if ((lemma = target as CoLemma) != null)
                {
                    result = new CoLemma(lemma.tok, lemma.Name, lemma.HasStaticKeyword, lemma.TypeArgs, lemma.Ins, lemma.Outs,
                        lemma.Req, lemma.Mod, lemma.Ens, decreases, lemma.Body, lemma.Attributes, lemma.SignatureEllipsis);

                } else
                    result = new Method(target.tok, target.Name, target.HasStaticKeyword, target.IsGhost, target.TypeArgs,
                        target.Ins, target.Outs, target.Req, target.Mod, target.Ens, decreases, target.Body, target.Attributes,
                        target.SignatureEllipsis);
                
                // register new method
                this.localContext.newTarget = result;
                globalContext.program.IncTotalBranchCount(globalContext.program.currentDebug);
            }

            solution_list.Add(new Solution(this.Copy()));
        }
    }
}
