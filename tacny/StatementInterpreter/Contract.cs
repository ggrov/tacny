using System;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny {
    class TacnyContract {
        private readonly Atomic atomic;

        public TacnyContract(Solution solution) {
            Contract.Requires(solution != null);
            // check if codeContracts for Tacny are enabled
            if (TacnyOptions.O.Contracts)
                atomic = solution.state;
        }

        public static void ValidateRequires(Solution solution) {
            Contract.Requires(solution != null);
            TacnyContract tc = new TacnyContract(solution);
            tc.ValidateRequires();
        }


        protected void ValidateRequires() {
            //if (atomic != null)
            //    foreach (var req in atomic.localContext.tactic.Req)
            //        ValidateOne(req);
        }

        protected void ValidateOne(MaybeFreeExpression mfe) {
            Contract.Requires<ArgumentNullException>(mfe != null);
            Expression expr = mfe.E;
            Expression res = null;

            atomic.ProcessArg(expr, out res);
            LiteralExpr result = res as LiteralExpr;
            Contract.Assert(result != null, Error.MkErr(expr, 1, "Boolean Expression"));
            Contract.Assert((bool)result.Value, Error.MkErr(expr, 14));
        }
    }
}
