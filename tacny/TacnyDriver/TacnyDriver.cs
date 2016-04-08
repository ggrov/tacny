using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using Microsoft.Dafny;
using Microsoft.Boogie;
using Tacny;
using System.Reflection;

namespace Main
{
    class TacnyDriver
    {

        enum ExitValue { VERIFIED = 0, PREPROCESSING_ERROR, DAFNY_ERROR, NOT_VERIFIED }
        static OutputPrinter printer; // console 
        const string PROG_ID = "main_program_id";

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args"></param>
        static int Main(string[] args)
        {
            int ret = 0;
            var thread = new System.Threading.Thread(
                new System.Threading.ThreadStart(() =>
                { ret = ExecuteTacny(args); }),

                0x10000000); // 256MB stack size to prevent stack

            thread.Start();
            thread.Join();

            return ret;
        }

        /// <summary>
        /// Worker thread.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static int ExecuteTacny(string[] args)
        {
            Contract.Requires(tcce.NonNullElements(args));
            //Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.Listeners.Clear();
            //Console.SetOut(TextWriter.Null);
            //Debug.AutoFlush = true;

            // install Dafny and Boogie commands
            var options = new Util.TacnyOptions();
            options.VerifySnapshots = 2;
            Util.TacnyOptions.Install(options);

            printer = new TacnyConsolePrinter();
            ExecutionEngine.printer = printer;

            ExitValue exitValue = ExitValue.VERIFIED;

            // parse command line args
            //Util.TacnyOptions.O.Parse(args);
            if (!CommandLineOptions.Clo.Parse(args))
            {
                exitValue = ExitValue.PREPROCESSING_ERROR;
                return (int)exitValue;
            }

            if (CommandLineOptions.Clo.Files.Count == 0)
            {
                printer.ErrorWriteLine(Console.Out, "*** Error: No input files were specified.");
                exitValue = ExitValue.PREPROCESSING_ERROR;
                return (int)exitValue;
            }
            Console.Out.WriteLine("BEGIN: Tacny Options");
            Util.TacnyOptions tc = Util.TacnyOptions.O;
            FieldInfo[] fields = tc.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (field.IsPublic)
                    Console.Out.WriteLine("# {0} : {1}", field.Name, field.GetValue(tc).ToString());
            }
            
            Console.Out.WriteLine("END: Tacny Options");
            exitValue = ProcessFiles(CommandLineOptions.Clo.Files);

            return (int)exitValue;
        }

        /// <summary>
        /// Processes the file
        /// </summary>
        /// <param name="fileNames"></param>
        /// <param name="lookForSnapshots"></param>
        /// <param name="programId"></param>
        /// <returns></returns>
        static ExitValue ProcessFiles(IList<string/*!*/>/*!*/ fileNames, bool lookForSnapshots = true, string programId = null)
        {
            Contract.Requires(tcce.NonNullElements(fileNames));
            string err;

            if (programId == null)
            {
                programId = PROG_ID;
            }

            ExitValue exitValue = ExitValue.VERIFIED;
            if (CommandLineOptions.Clo.VerifySeparately && 1 < fileNames.Count)
            {
                foreach (var f in fileNames)
                {
                    Console.WriteLine();
                    Console.WriteLine("-------------------- {0} --------------------", f);
                    var ev = ProcessFiles(new List<string> { f }, lookForSnapshots, f);
                    if (exitValue != ev && ev != ExitValue.VERIFIED)
                    {
                        exitValue = ev;
                    }
                }
                return exitValue;
            }

            using (XmlFileScope xf = new XmlFileScope(CommandLineOptions.Clo.XmlSink, fileNames[fileNames.Count - 1]))
            {
                Tacny.Program tacnyProgram;
                string programName = fileNames.Count == 1 ? fileNames[0] : "the program";
                // install Util.Printer
                try
                {
                    Debug.WriteLine("Initializing Tacny Program");
                    tacnyProgram = new Tacny.Program(fileNames, programId);
                    // Initialize the printer
                    Util.Printer.Install(fileNames[0]);
                    tacnyProgram.MaybePrintProgram(tacnyProgram.dafnyProgram, programName + "_src");
                }
                catch (ArgumentException ex)
                {
                    exitValue = ExitValue.DAFNY_ERROR;
                    printer.ErrorWriteLine(Console.Out, ex.Message);
                    return exitValue;
                }

                if (!CommandLineOptions.Clo.NoResolve && !CommandLineOptions.Clo.NoTypecheck && DafnyOptions.O.DafnyVerify)
                {

                    if (!Util.TacnyOptions.O.LazyEval)
                    {
                        Debug.WriteLine("Starting eager tactic evaluation");
                        Tacny.Interpreter r = new Tacny.Interpreter(tacnyProgram);

                        err = r.ResolveProgram();
                        if (err != null)
                        {
                            exitValue = ExitValue.DAFNY_ERROR;
                            printer.ErrorWriteLine(Console.Out, err);
                        }
                        else
                        {
                            tacnyProgram.PrintProgram();
                        }
                    }
                    else
                    {
                        var StartTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                        Debug.WriteLine("Starting lazy tactic evaluation");
                        LazyTacny.Interpreter r = new LazyTacny.Interpreter(tacnyProgram);
                        r.ResolveProgram();
                        var EndTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                        TextWriter tw = new System.IO.StreamWriter("_debug.dat", true);
                        //tacnyProgram.PrintDebugMessage((EndTime - StartTime).ToString(), tw);
                        tacnyProgram.PrintProgram();
                        Debug.WriteLine("Fnished lazy tactic evaluation");
                    }
                }
                tacnyProgram.PrintAllDebugData(Util.TacnyOptions.O.PrintCsv);
            }
            return exitValue;
        }

        class TacnyConsolePrinter : OutputPrinter
        {

            public void AdvisoryWriteLine(string format, params object[] args)
            {
            }

            public void ErrorWriteLine(TextWriter tw, string format, params object[] args)
            {
            }

            public void ErrorWriteLine(TextWriter tw, string s)
            {
            }

            public void Inform(string s, TextWriter tw)
            {
            }

            public void ReportBplError(IToken tok, string message, bool error, TextWriter tw, string category = null)
            {
            }

            public void WriteErrorInformation(ErrorInformation errorInfo, TextWriter tw, bool skipExecutionTrace = true)
            {
            }

            public void WriteTrailer(PipelineStatistics stats)
            {

            }
        }
    }
}
