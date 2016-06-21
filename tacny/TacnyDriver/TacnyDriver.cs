using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Util;
using Printer = Util.Printer;

namespace Tacny {
  public class TacnyDriver {

    enum ExitValue { Verified = 0, PreprocessingError, DafnyError/*NotVerified */}
    static OutputPrinter _printer; // console 
    public const string ProgId = "main_program_id";

    /// <summary>
    /// Main method
    /// </summary>
    /// <param name="args"></param>
    static int Main(string[] args) {
      var ret = 0;
      var thread = new Thread(
          () => { ret = ExecuteTacny(args); },

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
    public static int ExecuteTacny(string[] args) {

      Debug.Listeners.Clear();
      Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));

      Debug.AutoFlush = true;
      // install Dafny and Boogie commands
      var options = new TacnyOptions { VerifySnapshots = 2 };
      TacnyOptions.Install(options);
      _printer = new TacnyConsolePrinter();
      ExecutionEngine.printer = new ConsolePrinter();

      ExitValue exitValue;

      // parse command line args
      //Util.TacnyOptions.O.Parse(args);
      if (!CommandLineOptions.Clo.Parse(args)) {
        exitValue = ExitValue.PreprocessingError;
        return (int)exitValue;
      }

      if (CommandLineOptions.Clo.Files.Count == 0) {
        _printer.ErrorWriteLine(Console.Out, "*** Error: No input files were specified.");
        exitValue = ExitValue.PreprocessingError;
        return (int)exitValue;
      }
      Console.Out.WriteLine("BEGIN: Tacny Options");
      var tc = TacnyOptions.O;
      FieldInfo[] fields = tc.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
      foreach (var field in fields) {

        if (field.IsPublic) {
          if (field.Name == "EnableSearch") {
            var val = field.GetValue(tc);
            if (!(val is int)) continue;
            var tmp = (int)val;
            if (tmp < 0) continue;
            var name = (LazyTacny.Strategy)tmp;
            Console.Out.WriteLine($"# SearchStrategy : {name}");
          } else
            Console.Out.WriteLine($"# {field.Name} : {field.GetValue(tc)}");
        }

      }

      Console.Out.WriteLine("END: Tacny Options");
      exitValue = ProcessFiles(CommandLineOptions.Clo.Files);

      return (int)exitValue;
    }

    /// <summary>
    /// Processes the file
    /// </summary>
    /// <param name="fileNames"></param>
    /// <param name="programId"></param>
    /// <returns></returns>
    static ExitValue ProcessFiles(IList<string/*!*/>/*!*/ fileNames, string programId = null) {
      //Contract.Requires(Tacny.tcce.NonNullElements(fileNames));

      if (programId == null) {
        programId = ProgId;
      }

      var exitValue = ExitValue.Verified;
      if (CommandLineOptions.Clo.VerifySeparately && 1 < fileNames.Count) {
        foreach (string f in fileNames) {
          Console.WriteLine();
          Console.WriteLine($"-------------------- {f} --------------------");
          var ev = ProcessFiles(new List<string> { f }, f);
          if (exitValue != ev && ev != ExitValue.Verified) {
            exitValue = ev;
          }
        }
        return exitValue;
      }

      using (new XmlFileScope(CommandLineOptions.Clo.XmlSink, fileNames[fileNames.Count - 1])) {
        Program tacnyProgram;
        string programName = fileNames.Count == 1 ? fileNames[0] : "the program";
        // install Util.Printer
        try {
          Debug.WriteLine("Initializing Tacny Program");
          tacnyProgram = new Program(fileNames, programId);
          // Initialize the printer
          Printer.Install(fileNames[0]);
          tacnyProgram.MaybePrintProgram(tacnyProgram.DafnyProgram, programName + "_src");
        } catch (ArgumentException ex) {
          exitValue = ExitValue.DafnyError;
          _printer.ErrorWriteLine(Console.Out, ex.Message);
          return exitValue;
        }


        var program = tacnyProgram.ParseProgram();
        int qq = tacnyProgram.ResolveProgram(program);
        Interpreter.FindAndApplyTactic(tacnyProgram.ParseProgram(), ((ClassDecl) program.DefaultModuleDef.TopLevelDecls[0]).Members[0]);


        if (!CommandLineOptions.Clo.NoResolve && !CommandLineOptions.Clo.NoTypecheck && DafnyOptions.O.DafnyVerify) {

          Debug.WriteLine("Starting lazy tactic evaluation");
          LazyTacny.Interpreter r = new LazyTacny.Interpreter(tacnyProgram);
          var prog = r.ResolveProgram();
          tacnyProgram.PrintProgram(prog);
          Debug.WriteLine("Fnished lazy tactic evaluation");
        }
        tacnyProgram.PrintAllDebugData(TacnyOptions.O.PrintCsv);
      }
      return exitValue;
    }

    class TacnyConsolePrinter : OutputPrinter {

      public void AdvisoryWriteLine(string format, params object[] args) {
      }

      public void ErrorWriteLine(TextWriter tw, string format, params object[] args) {
      }

      public void ErrorWriteLine(TextWriter tw, string s) {
      }

      public void Inform(string s, TextWriter tw) {
      }

      public void ReportBplError(IToken tok, string message, bool error, TextWriter tw, string category = null) {
      }

      public void WriteErrorInformation(ErrorInformation errorInfo, TextWriter tw, bool skipExecutionTrace = true) {
      }

      public void WriteTrailer(PipelineStatistics stats) {

      }
    }
  }
}
