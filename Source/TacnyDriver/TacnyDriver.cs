using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;

namespace Tacny
{
    class TacnyDriver
    {

        enum ExitValue { VERIFIED = 0, PREPROCESSING_ERROR, DAFNY_ERROR, NOT_VERIFIED }
        static OutputPrinter printer; // console printer
        // debug 
        const bool DEBUG = true;
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
                { ret = ThreadMain(args); }),
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
        public static int ThreadMain(string[] args)
        {
            Contract.Requires(tcce.NonNullElements(args));

            printer = new TacnyConsolePrinter();
            ExecutionEngine.printer = printer;

            ExitValue exitValue = ExitValue.VERIFIED;

            TacnyOptions.Install(new TacnyOptions()); // prep Dafny/Boogie

            CommandLineOptions.Clo.RunningBoogieFromCommandLine = true;
            if (DEBUG)
                CommandLineOptions.Clo.Wait = true;

            // parse the file
            if (!CommandLineOptions.Clo.Parse(args))
            {
                exitValue = ExitValue.PREPROCESSING_ERROR;
                cleanup();
                return (int)exitValue;
            }

            if (CommandLineOptions.Clo.Files.Count == 0)
            {
                printer.ErrorWriteLine(Console.Out, "*** Error: No input files were specified.");
                exitValue = ExitValue.PREPROCESSING_ERROR;
                cleanup();
                return (int)exitValue;
            }
            if (CommandLineOptions.Clo.XmlSink != null)
            {
                string errMsg = CommandLineOptions.Clo.XmlSink.Open();
                if (errMsg != null)
                {
                    printer.ErrorWriteLine(Console.Out, "*** Error: " + errMsg);
                    exitValue = ExitValue.PREPROCESSING_ERROR;
                    cleanup();
                    return (int)exitValue;
                }
            }

            if (!CommandLineOptions.Clo.DontShowLogo)
            {
                Console.WriteLine(CommandLineOptions.Clo.Version);
            }

            if (CommandLineOptions.Clo.ShowEnv == CommandLineOptions.ShowEnvironment.Always)
            {
                Console.WriteLine("---Command arguments");
                foreach (string arg in args)
                {
                    Contract.Assert(arg != null);
                    Console.WriteLine(arg);
                }
                Console.WriteLine("--------------------");
            }

            foreach (string file in CommandLineOptions.Clo.Files)
            {
                Contract.Assert(file != null);
                string extension = Path.GetExtension(file);
                if (extension != null) { extension = extension.ToLower(); }
                if (extension != ".dfy")
                {
                    printer.ErrorWriteLine(Console.Out, "*** Error: '{0}': Filename extension '{1}' is not supported. Input files must be Dafny programs (.dfy).", file,
                        extension == null ? "" : extension);
                    exitValue = ExitValue.PREPROCESSING_ERROR;
                    cleanup();
                    return (int)exitValue;
                }
            }

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

            if (0 <= CommandLineOptions.Clo.VerifySnapshots && lookForSnapshots)
            {
                var snapshotsByVersion = ExecutionEngine.LookForSnapshots(fileNames);
                foreach (var s in snapshotsByVersion)
                {
                    var ev = ProcessFiles(new List<string>(s), false, programId);
                    if (exitValue != ev && ev != ExitValue.VERIFIED)
                    {
                        exitValue = ev;
                    }
                }
                return exitValue;
            }

            using (XmlFileScope xf = new XmlFileScope(CommandLineOptions.Clo.XmlSink, fileNames[fileNames.Count - 1]))
            {
                Dafny.Program dafnyProgram;
                string programName = fileNames.Count == 1 ? fileNames[0] : "the program";
                string err = Tacny.Main.ParseCheck(fileNames, programName, out dafnyProgram);
                if (err != null)
                {
                    exitValue = ExitValue.DAFNY_ERROR;
                    printer.ErrorWriteLine(Console.Out, err);
                }
                else if (dafnyProgram != null && !CommandLineOptions.Clo.NoResolve && !CommandLineOptions.Clo.NoTypecheck
                  && DafnyOptions.O.DafnyVerify)
                {
                    PipelineStatistics stats;

                    err = Tacny.Main.ResolveTactics(ref dafnyProgram, fileNames, programId, out stats);
                    if (err != null)
                    {
                        exitValue = ExitValue.DAFNY_ERROR;
                        printer.ErrorWriteLine(Console.Out, err);
                    }
                    else
                    {
                        compile_dfy(dafnyProgram, fileNames, stats);
                    }
                }
            }
            return exitValue;
        }

        private static void compile_dfy(Dafny.Program dafnyProgram, IList<string> fileNames, PipelineStatistics stats)
        {
            //printer.WriteTrailer(stats);
            if ((DafnyOptions.O.Compile /*&& allOk*/ && CommandLineOptions.Clo.ProcsToCheck == null) || DafnyOptions.O.ForceCompile)
                Dafny.DafnyDriver.CompileDafnyProgram(dafnyProgram, fileNames[0]);
        }

        /// <summary>
        /// Clean up before exiting
        /// </summary>
        static void cleanup()
        {
            if (CommandLineOptions.Clo.XmlSink != null)
            {
                CommandLineOptions.Clo.XmlSink.Close();
            }
            if (CommandLineOptions.Clo.Wait)
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
        }


        class TacnyConsolePrinter : ConsolePrinter
        {
            public override void ReportBplError(IToken tok, string message, bool error, TextWriter tw, string category = null)
            {
                base.ReportBplError(tok, message, error, tw, category);

                if (tok is Dafny.NestedToken)
                {
                    var nt = (Dafny.NestedToken)tok;
                    ReportBplError(nt.Inner, "Related location", false, tw);
                }
            }
        }
    }
}
