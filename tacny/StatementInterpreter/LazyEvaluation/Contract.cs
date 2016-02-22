using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;

namespace LazyTacny
{
    class TacnyContract
    {
        private readonly Atomic atomic;

        public TacnyContract(Solution solution)
        {
            Contract.Requires(solution != null);
            // check if codeContracts for Tacny are enabled
            if (Util.TacnyOptions.O.Contracts)
                this.atomic = solution.state;
        }

        public static void ValidateRequires(Solution solution)
        {
            Contract.Requires(solution != null);
            TacnyContract tc = new TacnyContract(solution);
            tc.ValidateRequires();
        }


        protected void ValidateRequires()
        {
            if (atomic != null)
                foreach (var req in atomic.localContext.tac.Req)
                    ValidateOne(req);
        }

        protected void ValidateOne(MaybeFreeExpression mfe)
        {
            Contract.Requires<ArgumentNullException>(mfe != null);
            Expression expr = mfe.E;
            Expression res = null;

            foreach (var item in atomic.ProcessStmtArgument(expr))
            {
                Dafny.LiteralExpr result = item as Dafny.LiteralExpr;
                Contract.Assert(result != null, Util.Error.MkErr(expr, 1, "Boolean Expression"));
                Contract.Assert((bool)result.Value, Util.Error.MkErr(expr, 14));
            }
        }
    }
}
