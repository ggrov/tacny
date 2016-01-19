using System;
using System.IO;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using Microsoft.Boogie;

namespace Util
{
    public class Printer : Dafny.Printer
    {
        TextWriter wr;
        DafnyOptions.PrintModes printMode;
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
            PrintTopLevelDecls(prog.DefaultModuleDef.TopLevelDecls, 0, Path.GetFullPath(prog.FullName));
            wr.Flush();
        }

        public void PrintDebugMessage(string message, string program_name, params object[] args)
        {
            //wr.WriteLine("*DEBUG DATA*");
            //wr.WriteLine("Program: {0}", program_name);
            wr.WriteLine(String.Format(message, args));
            wr.Flush();
        }

        public static void Error(string msg, params object[] args)
        {
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(string.Format(msg, args));
            Console.ForegroundColor = col;
        }

        public static void Error(IToken tok, string msg, params object[] args)
        {
            Contract.Requires(tok != null);
            Contract.Requires(msg != null);
            Error("{0}({1},{2}): Error: {3}",
                DafnyOptions.Clo.UseBaseNameForFileName ? System.IO.Path.GetFileName(tok.filename) : tok.filename, tok.line, tok.col - 1,
                string.Format(msg, args));
        }

        public static void Error(Dafny.Declaration d, string msg, params object[] args)
        {
            Contract.Requires(d != null);
            Contract.Requires(msg != null);
            Error(d.tok, msg, args);
        }

        public static void Error(Statement s, string msg, params object[] args)
        {
            Contract.Requires(s != null);
            Contract.Requires(msg != null);
            Error(s.Tok, msg, args);
        }

        public static void Error(NonglobalVariable v, string msg, params object[] args)
        {
            Contract.Requires(v != null);
            Contract.Requires(msg != null);
            Error(v.tok, msg, args);
        }

        public static void Error(Expression e, string msg, params object[] args)
        {
            Contract.Requires(e != null);
            Contract.Requires(msg != null);
            Error(e.tok, msg, args);
        }

        public static void Warning(IToken tok, string msg, params object[] args)
        {
            Contract.Requires(tok != null);
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Warning("{0}({1},{2}): Warning: {3}",
                DafnyOptions.Clo.UseBaseNameForFileName ? System.IO.Path.GetFileName(tok.filename) : tok.filename, tok.line, tok.col - 1,
                string.Format(msg, args));
            Console.ForegroundColor = col;
        }

        public static void Warning(string msg, params object[] args)
        {
            Contract.Requires(msg != null);
            ConsoleColor col = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Format(msg, args));
            Console.ForegroundColor = col;
        }

    }
}
