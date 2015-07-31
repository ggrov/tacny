using System.IO;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace Tacny
{
    class Printer : Dafny.Printer
    {
        TextWriter wr;
        DafnyOptions.PrintModes printMode;

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
    }
}
