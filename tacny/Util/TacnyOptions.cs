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

        public bool ResolveTactics = true;
        public bool Debug = false;
        public bool Contracts = true;
        public bool EvalAnalysis = true;
        public bool ParallelExecution = false;
        public bool LazyEval = true;

        protected override bool ParseOption(string name, Bpl.CommandLineOptionEngine.CommandLineParseState ps)
        {
            var args = ps.args;
            int i = 0;
            switch (name)
            {
                case "restactics":
                    this.ResolveTactics = false;
                    return true;
                case "debug":
                    this.Debug = true;
                    return true;
                case "contracts":                    
                    if (ps.GetNumericArgument(ref i, 1))
                        this.Contracts = i == 1;
                    return true;
                case "evalAnalysis":
                    if (ps.GetNumericArgument(ref i, 1))
                        this.EvalAnalysis = i == 1;
                    return true;
                case "parallel":
                    if (ps.GetNumericArgument(ref i, 1))
                        this.ParallelExecution = i == 1;
                    return true;
                case "lazy":
                    if (ps.GetNumericArgument(ref i, 1))
                        this.LazyEval = i == 1;
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
