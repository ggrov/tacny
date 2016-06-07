using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Bpl = Microsoft.Boogie;
using Util;

namespace Tacny {
    public class Pipeline {
        private static Mutex mut = new Mutex();

        public static void PrintBoogieProgram(Dafny.Program program)
        {
          
            Bpl.Program boogieProgram;
            Translate(program, Thread.CurrentThread.Name, out boogieProgram);
            ExecutionEngine.PrintBplFile($"{program.Name}.bpl", boogieProgram, true, true, true);
            var prog = ExecutionEngine.ParseBoogieProgram(new List<string>() { $"{program.Name}.bpl" }, false);
            var p = new Pipeline();
            p.StartBoogie($"{program.Name}.bpl");
            PipelineStatistics t;
            List<ErrorInformation> err;
            p.BoogiePipeline(boogieProgram, new List<string>() { $"{program.Name}.bpl" }, "name", out t, out err);

        }



        public void StartBoogie(string fileName) {
          var processInfo = new ProcessStartInfo(GetBoogiePath())
          {
            Arguments = $"{Path.Combine(Directory.GetCurrentDirectory(), fileName)}",
            WindowStyle = ProcessWindowStyle.Hidden
          };
          Process process = new Process();
            process.StartInfo = processInfo;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            var reader = process.StandardOutput;
            string output = reader.ReadToEnd();
            System.Console.WriteLine(output);
            process.WaitForExit();
            process.Close();
        }

        public static string GetBoogiePath()
        {
            var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(path), "boogie.exe");
        }

        public PipelineOutcome VerifyProgram(Dafny.Program program, IList<string> fileNames, string programId, out PipelineStatistics stats, out List<ErrorInformation> errorList, out ErrorInformation errorInfo) {
            Microsoft.Boogie.Program boogieProgram;
            Translate(program, Thread.CurrentThread.Name, out boogieProgram);
            var po = BoogiePipeline(boogieProgram, fileNames, programId, out stats, out errorList);
            errorInfo = errorList.FirstOrDefault();

            return po;
        }


        /// <summary>
        /// Translates Dafny program to Boogie program
        /// </summary>
        /// <returns>Exit value</returns>
        public static void Translate(Dafny.Program dafnyProgram, string uniqueIdPrefix, out Bpl.Program boogieProgram) {

            Translator translator = new Translator(dafnyProgram.reporter);
            translator.InsertChecksums = true;
            translator.UniqueIdPrefix = uniqueIdPrefix;
            boogieProgram = translator.Translate(dafnyProgram);
        }

        /// <summary>
        /// Pipeline the boogie program to Dafny where it is valid
        /// </summary>
        /// <returns>Exit value</returns>
        public PipelineOutcome BoogiePipeline(Bpl.Program program, IList<string> fileNames, string programId, out PipelineStatistics stats, out List<ErrorInformation> errorList) {
            Contract.Requires(program != null);
            Contract.Ensures(0 <= Contract.ValueAtReturn(out stats).InconclusiveCount && 0 <= Contract.ValueAtReturn(out stats).TimeoutCount);

            LinearTypeChecker ltc;
            CivlTypeChecker ctc;
            string baseName = cce.NonNull(Path.GetFileName(fileNames[fileNames.Count - 1]));
            baseName = cce.NonNull(Path.ChangeExtension(baseName, "bpl"));
            string bplFileName = Path.Combine(Path.GetTempPath(), baseName);

            errorList = new List<ErrorInformation>();
            stats = new PipelineStatistics();

            if (TacnyOptions.O.ParallelExecution)
                mut.WaitOne();

            PipelineOutcome oc = ExecutionEngine.ResolveAndTypecheck(program, bplFileName, out ltc, out ctc);
            switch (oc) {
                case PipelineOutcome.ResolvedAndTypeChecked:
                    ExecutionEngine.EliminateDeadVariables(program);
                    ExecutionEngine.CollectModSets(program);
                    ExecutionEngine.CoalesceBlocks(program);
                    ExecutionEngine.Inline(program);
                    errorList = new List<ErrorInformation>();
                    var tmp = new List<ErrorInformation>();

                    oc = ExecutionEngine.InferAndVerify(program, stats, programId, errorInfo => {
                        tmp.Add(errorInfo);
                    });
                    errorList.AddRange(tmp);
                    if (TacnyOptions.O.ParallelExecution)
                        mut.ReleaseMutex();
                    return oc;
                default:
                    if (TacnyOptions.O.ParallelExecution)
                        mut.ReleaseMutex();
                    return oc;
                    //Contract.Assert(false); throw new cce.UnreachableException();  // unexpected outcome
            }
        }
    }
}
