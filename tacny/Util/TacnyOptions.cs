using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Bpl = Microsoft.Boogie;

namespace Util
{
    public class TacnyOptions : DafnyOptions
    {
        public TacnyOptions()
            : base()
        {

        }

        private static TacnyOptions clo;
        public static new TacnyOptions O
        {
            get { return clo; }
        }
        public static void Install(TacnyOptions options)
        {
            Contract.Requires(options != null);
            clo = options;
            DafnyOptions.Install(options);
        }
        public bool Contracts = true;
        public bool EvalAnalysis = true;
        public bool ParallelExecution = false;
        public bool LazyEval = true;
        public bool PrintCsv = false;
        public bool EnableSearch = true;
        protected override bool ParseOption(string name, Bpl.CommandLineOptionEngine.CommandLineParseState ps)
        {
            var args = ps.args;
            switch (name)
            {
                case "contracts":
                    int i = 0;
                    if (ps.GetNumericArgument(ref i, 2))
                        this.Contracts = i == 1;
                    return true;
                case "evalAnalysis":
                    int j = 0;
                    if (ps.GetNumericArgument(ref j, 2))
                        this.EvalAnalysis = j == 1;
                    return true;
                case "parallel":
                    int k = 0;
                    if (ps.GetNumericArgument(ref k, 2))
                        this.ParallelExecution = k == 1;
                    return true;
                case "lazy":
                    int l = 0;
                    if (ps.GetNumericArgument(ref l, 2))
                        this.LazyEval = l == 1;
                    return true;
                case "printCsv":
                    int m = 0;
                    if (ps.GetNumericArgument(ref m, 2))
                        this.PrintCsv = m == 1;
                    return true;
                case "search":
                    int s = 0;
                    if (ps.GetNumericArgument(ref s, 2))
                        this.EnableSearch = s == 1;
                    return true;
                default:
                    break;
            }

            return base.ParseOption(name, ps);
        }

        public override void Usage()
        {
            base.Usage();
            Console.WriteLine(@"--- Tacny options ---------------------------------------------------
                    /contracts:<n>
                            0 - disable Tacny code contracts
                            1 - (default) enable Tacny code contracts");
        }
    }
}
