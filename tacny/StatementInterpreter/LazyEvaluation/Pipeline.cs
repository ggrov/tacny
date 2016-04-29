using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;

namespace Tacny
{
    public class Pipeline
    {
        private static Mutex mut = new Mutex();

        public static PipelineOutcome VerifyProgram(Dafny.Program program, IList<string> fileNames, string programId, out PipelineStatistics stats, out List<ErrorInformation> errorList, out ErrorInformation errorInfo)
        {
            Microsoft.Boogie.Program boogieProgram;
            Translate(program, fileNames, programId, Thread.CurrentThread.Name, out boogieProgram);
            var po = BoogiePipeline(boogieProgram, fileNames, programId, out stats, out errorList);
            errorInfo = errorList.FirstOrDefault();

            return po;
        }


        /// <summary>
        /// Translates Dafny program to Boogie program
        /// </summary>
        /// <returns>Exit value</returns>
        private static void Translate(Dafny.Program dafnyProgram, IList<string> fileNames, string programId, string uniqueIdPrefix, out Microsoft.Boogie.Program boogieProgram)
        {

            Dafny.Translator translator = new Dafny.Translator();
            translator.InsertChecksums = true;
            translator.UniqueIdPrefix = uniqueIdPrefix;
            boogieProgram = translator.Translate(dafnyProgram);
        }

        /// <summary>
        /// Pipeline the boogie program to Dafny where it is valid
        /// </summary>
        /// <returns>Exit value</returns>
        private static PipelineOutcome BoogiePipeline(Microsoft.Boogie.Program program, IList<string> fileNames, string programId, out PipelineStatistics stats, out List<ErrorInformation> errorList)
        {
            Contract.Requires(program != null);
            Contract.Ensures(0 <= Contract.ValueAtReturn(out stats).InconclusiveCount && 0 <= Contract.ValueAtReturn(out stats).TimeoutCount);

            LinearTypeChecker ltc;
            MoverTypeChecker mtc;
            string baseName = cce.NonNull(Path.GetFileName(fileNames[fileNames.Count - 1]));
            baseName = cce.NonNull(Path.ChangeExtension(baseName, "bpl"));
            string bplFileName = Path.Combine(Path.GetTempPath(), baseName);

            errorList = new List<ErrorInformation>();
            stats = new PipelineStatistics();
            
            if (Util.TacnyOptions.O.ParallelExecution)
                mut.WaitOne();

            PipelineOutcome oc = ExecutionEngine.ResolveAndTypecheck(program, bplFileName, out ltc, out mtc);
            switch (oc)
            {
                case PipelineOutcome.ResolvedAndTypeChecked:
                    ExecutionEngine.EliminateDeadVariables(program);
                    ExecutionEngine.CollectModSets(program);
                    ExecutionEngine.CoalesceBlocks(program);
                    ExecutionEngine.Inline(program);
                    errorList = new List<ErrorInformation>();
                    var tmp = new List<ErrorInformation>();
                    //return ExecutionEngine.InferAndVerify(program, stats, programId);

                    oc = ExecutionEngine.InferAndVerify(program, stats, programId, errorInfo =>
                    {
                        tmp.Add(errorInfo);
                    });
                    errorList.AddRange(tmp);
                    if (Util.TacnyOptions.O.ParallelExecution)
                        mut.ReleaseMutex();
                    return oc;
                default:
                    if (Util.TacnyOptions.O.ParallelExecution)
                        mut.ReleaseMutex();
                    return oc;
                    //Contract.Assert(false); throw new cce.UnreachableException();  // unexpected outcome
            }
        }
    }
}
