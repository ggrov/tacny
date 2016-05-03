using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;

namespace LazyTacny
{
    class PostcondAtomic : Atomic, IAtomicLazyStmt
    {
        public PostcondAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            yield return Postcond(st, solution);
            yield break;
        }


        private Solution Postcond(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<Expression> ensures = null;
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));

            Contract.Assert(call_arguments.Count <= 1, Util.Error.MkErr(st, 0, 0, call_arguments.Count));
            MemberDecl memberDecl = null;
            if (call_arguments.Count > 0)
            {
                foreach (var member in ProcessStmtArgument(call_arguments[0]))
                {
                    memberDecl = member as MemberDecl;
                    if (memberDecl == null)
                        Contract.Assert(false, Util.Error.MkErr(st, 1, "Function, [Ghost] Method, Declaration"));
                    break;
                }
            }
            else
            {
                memberDecl = StaticContext.md;
            }

            Function fun = memberDecl as Function;
            if (fun != null)
            {
                foreach (var req in fun.Ens)
                    ensures.Add(Util.Copy.CopyExpression(req));
            }
            else
            {

                Method method = memberDecl as Method;
                if (method != null)
                {
                    foreach (var req in method.Ens)
                        ensures.Add(Util.Copy.CopyExpression(req.E));
                }
                else
                {
                    Contract.Assert(false, Util.Error.MkErr(st, 1, "Function, [Ghost] Method, Declaration"));
                }
            }
            var ac = this.Copy();
            ac.AddLocal(lv, ensures);
            return new Solution(ac);
        }
    }
}
