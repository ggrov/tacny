using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;

namespace LazyTacny
{
    public class GetMemberAtomic : Atomic, IAtomicLazyStmt
    {
        public GetMemberAtomic(Atomic atomic) : base(atomic) { }
        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            yield return GetMember(st, solution);
            yield break;
        }

        private Solution GetMember(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments;
            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 1), Util.Error.MkErr(st, 0, 1, call_arguments.Count));
            MemberDecl memberDecl = null;
            var memberName = call_arguments[0] as StringLiteralExpr;

            try
            {
                memberDecl = staticContext.program.members[memberName.AsStringLiteral()];
            } catch (KeyNotFoundException e)
            {
                Contract.Assert(false, Util.Error.MkErr(st, 20, memberName.AsStringLiteral()));
            }

            var ac = this.Copy();
            ac.AddLocal(lv, memberDecl);
            return new Solution(ac);
        }
    }
}
