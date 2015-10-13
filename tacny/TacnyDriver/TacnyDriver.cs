#define DEBUG

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;

namespace Tacny
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
            // measure execution time
           
            Contract.Requires(tcce.NonNullElements(args));

            printer = new TacnyConsolePrinter();
            ExecutionEngine.printer = printer;

            ExitValue exitValue = ExitValue.VERIFIED;

            TacnyOptions.Install(new TacnyOptions()); // prep Dafny/Boogie

            CommandLineOptions.Clo.RunningBoogieFromCommandLine = true;
            #if DEBUG
            CommandLineOptions.Clo.Wait = true;
            #endif
                

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
                Tacny.Program tacnyProgram;
                string programName = fileNames.Count == 1 ? fileNames[0] : "the program";
                try
                {
                    tacnyProgram = new Tacny.Program(fileNames, programId);
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
                    Interpreter r = new Interpreter(tacnyProgram);

                    err = r.ResolveProgram();
                    if (err != null)
                    {
                        exitValue = ExitValue.DAFNY_ERROR;
                        printer.ErrorWriteLine(Console.Out, err);
                    }
                    else
                    {
                        tacnyProgram.Print();
                    }
                }
            }
            return exitValue;
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
