using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Tacny;
namespace LazyTacny
{
    class FreshNameAtomic : Atomic, IAtomicLazyStmt
    {

        const string SUFFIX = "_";
        public FreshNameAtomic(Atomic atomic) : base(atomic)
        {

        }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            return FreshName(st, solution);
        }


        private IEnumerable<Solution> FreshName(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> callArguments = null;

            InitArgs(st, out lv, out callArguments);
            Contract.Assert(tcce.OfSize(callArguments, 1), Util.Error.MkErr(st, 0, 1, callArguments.Count));

            var nameLiteralExpr = callArguments[0] as StringLiteralExpr;
            Contract.Assert(nameLiteralExpr != null, Util.Error.MkErr(st, 1, "string"));
            var count = StaticContext.program.members.Keys.Count(x => x == nameLiteralExpr.AsStringLiteral());
            if(count == 0) {
                yield return AddNewLocal<StringLiteralExpr>(lv, Util.Copy.CopyStringLiteralExpr(nameLiteralExpr));
            } else
            {
                var nameValue = nameLiteralExpr.AsStringLiteral();
                var freshNameValue = string.Format("{0}{1}{2}", nameValue, SUFFIX, count);
                yield return AddNewLocal<StringLiteralExpr>(lv, new StringLiteralExpr(nameLiteralExpr.tok, freshNameValue, nameLiteralExpr.IsVerbatim));
            }
            yield break;
        }
    }
}
