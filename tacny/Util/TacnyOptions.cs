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

        protected override bool ParseOption(string name, Bpl.CommandLineOptionEngine.CommandLineParseState ps)
        {
            var args = ps.args;

            switch (name)
            {
                case "restactics":
                    this.ResolveTactics = false;
                    return true;
                case "debug":
                    this.Debug = true;
                    return true;
                case "contracts":
                    int contracts = 0;
                    if (ps.GetNumericArgument(ref contracts, 1))
                        this.Contracts = contracts == 1;
                    return true;
                    break;
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
