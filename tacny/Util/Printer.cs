using System;
using System.IO;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics;
using Microsoft.Boogie;

namespace Util
{
    public class Printer : Dafny.Printer
    {
        // Print the program
        private TextWriter wr;
        private TextWriter debugWriter; // debug writer
        private TextWriter csvWriter; // csv writer

        DafnyOptions.PrintModes printMode;
        const string DEBUG_SUFFIX = "data";
        const string FILENAME = "tacny";
        const string DEBUG_FOLDER = "Debug";
        private string FileName = null;
        private static Printer p;
        public static Printer P { get { return p; } }

        public static void Install(string filename)
        {
            Contract.Requires(filename != null);
            var fn = filename;
            if (fn.Contains("\\"))
            {
                int index = fn.LastIndexOf("\\");
                fn = fn.Substring(fn.LastIndexOf("\\") + 1);
            }

            if (fn.LastIndexOf(".") >= 0)
                fn = fn.Substring(0, fn.LastIndexOf("."));
            try {
                var tw = new System.IO.StreamWriter(fn + ".dfy");
                p = new Printer(tw, fn, TacnyOptions.O.PrintMode);
            } catch(IOException e)
            {
                Debug.WriteLine(e.Message);
                var tw = Console.Out;
                p = new Printer(tw, fn, TacnyOptions.O.PrintMode);
            }
        }
        protected Printer(TextWriter tw, string filename, DafnyOptions.PrintModes printMode = DafnyOptions.PrintModes.Everything) : base(tw, printMode)
        {
            this.FileName = filename;
            this.wr = tw;
            this.printMode = printMode;
        }

        [ContractInvariantMethod]
        protected void ObjectInvariant()
        {
            Contract.Invariant(wr != null);
        }

        private void InitializeDebugWriter(bool clearDirectory = true)
        {
            // check if folder exists
            var working_path = Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER);
            // delete old debug data
            if (!Directory.Exists(working_path))
                Directory.CreateDirectory(working_path);
            debugWriter = new StreamWriter(Path.Combine(working_path, string.Format("{0}.{1}", FileName, DEBUG_SUFFIX)), true);
        }

        public Dafny.Printer GetConsolePrinter() {
            return new Dafny.Printer(Console.Out);
        }

        private void InitializeCsvWriter()
        {
            // check if folder exists

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER)))
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER));
            if(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER, string.Format("{0}.{1}", FileName, "csv"))))
            {
                File.Delete(Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER, string.Format("{0}.{1}", FileName, "csv")));
            }
            csvWriter = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), DEBUG_FOLDER, string.Format("{0}.{1}", FileName, "csv")), true);
        }

        private void ClearStream(ref TextWriter tw)
        {
            tw.Close();
            tw = null;
        }

        new public void PrintProgram(Dafny.Program prog)
        {
            Contract.Requires(prog != null);
            wr = new System.IO.StreamWriter(prog.FullName + ".dfy");
            var printer = new Dafny.Printer(wr, DafnyOptions.PrintModes.Everything);
            printer.PrintTopLevelDecls(prog.DefaultModuleDef.TopLevelDecls, 0, prog.FullName);
            //printer.PrintProgram(prog);// PrintTopLevelDecls(prog.DefaultModuleDef.TopLevelDecls, 0, Path.GetFullPath(prog.FullName));
            wr.Flush();
        }

        public void PrintDebugMessage(string message, params object[] args)
        {
            if (debugWriter == null)
                InitializeDebugWriter();
            debugWriter.WriteLine(string.Format(message, args));
            debugWriter.Flush();
        }

        public void PrintCsvData(string data)
        {
            if (csvWriter == null)
                InitializeCsvWriter();

            csvWriter.Write(data);
            csvWriter.Flush();
            //ClearStream(ref csvWriter);
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
