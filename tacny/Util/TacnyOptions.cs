using System;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
namespace Util
{
    public class TacnyOptions : DafnyOptions
    {
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
        public bool ParallelExecution;
        public bool LazyEval = true;
        public bool PrintCsv;
        public int EnableSearch = -1;
        protected override bool ParseOption(string name, CommandLineParseState ps)
        {
            var args = ps.args;
            switch (name)
            {
                case "contracts":
                    int i = 0;
                    if (ps.GetNumericArgument(ref i, 2))
                        Contracts = i == 1;
                    return true;
                case "evalAnalysis":
                    int j = 0;
                    if (ps.GetNumericArgument(ref j, 2))
                        EvalAnalysis = j == 1;
                    return true;
                case "parallel":
                    int k = 0;
                    if (ps.GetNumericArgument(ref k, 2))
                        ParallelExecution = k == 1;
                    return true;
                case "lazy":
                    int l = 0;
                    if (ps.GetNumericArgument(ref l, 2))
                        LazyEval = l == 1;
                    return true;
                case "printCsv":
                    int m = 0;
                    if (ps.GetNumericArgument(ref m, 2))
                        PrintCsv = m == 1;
                    return true;
                case "search":
                    int s = -1;

                    if (ps.GetNumericArgument(ref s, 2))
                        EnableSearch = s;
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
