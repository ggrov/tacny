using System;
using System.IO;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace Util
{
    public class Printer : Dafny.Printer
    {
        TextWriter wr;
        DafnyOptions.PrintModes printMode;
        TextWriter debug_writer = null;
        const string SUFFIX = "_dbg";
        const string FILENAME = "tacny";
        private string filename = null;

        public Printer(TextWriter wr, DafnyOptions.PrintModes printMode = DafnyOptions.PrintModes.Everything)
            : base(wr, printMode)
        {
            Contract.Requires(wr != null);
            this.wr = wr;
            this.printMode = printMode;
        }

        public void PrintProgram(Dafny.Program prog)
        {
            Contract.Requires(prog != null);
            if (Bpl.CommandLineOptions.Clo.ShowEnv != Bpl.CommandLineOptions.ShowEnvironment.Never)
            {
                wr.WriteLine("// " + Bpl.CommandLineOptions.Clo.Version);
                wr.WriteLine("// " + Bpl.CommandLineOptions.Clo.Environment);
            }
            filename = prog.Name;
            wr.WriteLine("// {0}", prog.Name);
            //if (DafnyOptions.O.DafnyPrintResolvedFile != null)
            //{
            //    wr.WriteLine();
            //    wr.WriteLine("/*");
            //    base.PrintModuleDefinition(prog.BuiltIns.SystemModule, 0, Path.GetFullPath(DafnyOptions.O.DafnyPrintResolvedFile));
            //    wr.WriteLine("*/");
            //}
            //wr.WriteLine();
            //PrintCallGraph(prog.DefaultModuleDef, 0);
            PrintTopLevelDecls(prog.DefaultModuleDef.TopLevelDecls, 0, Path.GetFullPath(prog.FullName));
            wr.Flush();
        }

        public void PrintDebugMessage(string message, params object[] args)
        {
            wr.WriteLine("*DEBUG DATA*");
            wr.WriteLine("Program: {0}", wr.ToString());
            wr.WriteLine(String.Format(message, args));
            wr.Flush();
        }
    }
}
