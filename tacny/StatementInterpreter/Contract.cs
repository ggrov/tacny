using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;

namespace Tacny
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
                foreach (var req in atomic.localContext.tactic.Req)
                    ValidateOne(req);
        }

        protected void ValidateOne(MaybeFreeExpression mfe)
        {
            Contract.Requires<ArgumentNullException>(mfe != null);
            Expression expr = mfe.E;
            Expression res = null;

            atomic.ProcessArg(expr, out res);
            Dafny.LiteralExpr result = res as Dafny.LiteralExpr;
            Contract.Assert(result != null, Util.Error.MkErr(expr, 1, "Boolean Expression"));
            Contract.Assert((bool)result.Value, Util.Error.MkErr(expr, 14));
        }
    }
}
