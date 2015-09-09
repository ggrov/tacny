using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
namespace Tacny
{
    class TryAllAtomic : Atomic, IAtomicStmt
    {
        public TryAllAtomic(Atomic atomic)
            : base(atomic)
        { }

        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return TryAll(st, ref solution_list);
        }


        private string TryAll(Statement st, ref List<Solution> solution_list)
        {
            List<List<Expression>> result = new List<List<Expression>>();
            List<List<IVariable>> args = new List<List<IVariable>>();
            List<Expression> call_arguments = null;
            Expression md_signature;
            MemberDecl md;
            string err;
            Method m;

            err = InitArgs(st, out call_arguments);
            if (err != null) return err;

            md_signature = call_arguments[0];

            err = FindMd(md_signature, out md);
            if (err != null) return err;

            m = md as Method;
            if (m == null) return "Member declaration " + md_signature + " not found";

            int i = 0;
            foreach (var item in m.Ins)
            {
                args.Add(new List<IVariable>());
                foreach (var local_decl in globalContext.global_variables)
                {
                    Dafny.LocalVariable lv;
                    if (local_decl.Value is Dafny.LocalVariable)
                    {
                        lv = local_decl.Value as Dafny.LocalVariable;
                        Dafny.InferredTypeProxy itp = lv.OptionalType as InferredTypeProxy;
                        if (itp != null)
                        {
                            if (itp.T == null)
                                args[i].Add(local_decl.Value);
                        }
                        else
                        {
                            if (item.Type.Equals(lv.OptionalType))
                            {
                                args[i].Add(local_decl.Value);
                            }
                        }

                    }

                    else if (item.Type.Equals(local_decl.Value.Type))
                        args[i].Add(local_decl.Value);
                }
                /*
                foreach (var local_decl in globalContext.temp_variables)
                {
                    Dafny.LocalVariable lv;
                    if (local_decl.Value is Dafny.LocalVariable)
                    {
                        lv = local_decl.Value as Dafny.LocalVariable;
                        Dafny.InferredTypeProxy itp = lv.OptionalType as InferredTypeProxy;
                        if (itp != null)
                        {
                            if (itp.T == null)
                                args[i].Add(local_decl.Value);
                        }
                        else
                        {
                            if (item.Type.Equals(lv.OptionalType))
                            {
                                args[i].Add(local_decl.Value);
                            }
                        }

                    }

                    else if (item.Type.Equals(local_decl.Value.Type))
                        args[i].Add(local_decl.Value);
                }
                */
                i++;
            }

            GeneratePremutations(args, 0, new List<Expression>(), ref result);

            if (result.Count == 0)
            {
                ApplySuffix aps = new ApplySuffix(md_signature.tok, md_signature, new List<Expression>());
                UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                Atomic ac = this.Copy();
                ac.AddUpdated(us, us);
                solution_list.Add(new Solution(ac));
            }
            else
            {
                foreach (var item in result)
                {
                    ApplySuffix aps = new ApplySuffix(md_signature.tok, md_signature, item);
                    UpdateStmt us = new UpdateStmt(aps.tok, aps.tok, new List<Expression>(), new List<AssignmentRhs>() { new ExprRhs(aps) });
                    Atomic ac = this.Copy();
                    ac.AddUpdated(us, us);
                    solution_list.Add(new Solution(ac));
                }
            }
            return null;
        }

        private string FindMd(Expression md_signature, out MemberDecl md)
        {
            md = null;
            NameSegment ns = md_signature as NameSegment;
            if (ns == null) return "FindMd: Unsupported argument. Expected NameSegment received " + md_signature.GetType();
            md = program.GetTactic(ns.Name);
            if (md != null) return null;
            md = program.GetMember(ns.Name);
            if (md != null) return null;

            return "Member with name " + ns.Name + " is not defined";
        }

        private void GeneratePremutations(List<List<IVariable>> args, int depth, List<Expression> current, ref List<List<Expression>> result)
        {
            if (args.Count == 0) return;
            if (depth == args.Count)
            {
                result.Add(current);
                return;
            }

            for (int i = 0; i < args[depth].Count; ++i)
            {
                List<Expression> tmp = new List<Expression>();
                tmp.AddRange(current);
                IVariable iv = args[depth][i];
                NameSegment ns = new NameSegment(iv.Tok, iv.Name, null);
                tmp.Add(ns);
                GeneratePremutations(args, depth + 1, tmp, ref result);
            }
        }
    }
}
