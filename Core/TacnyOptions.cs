using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics.Contracts;
using Bpl = Microsoft.Boogie;

namespace Tacny
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
                default: 
                    break;
            }

            return base.ParseOption(name, ps);
        }

        public override void Usage()
        {
            base.Usage();
            Console.WriteLine(@"--- Tacny options ---------------------------------------------------
                    /restactics
                                Disable tactic resolution");
        }
    }
}
