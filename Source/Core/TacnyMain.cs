using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using System.Diagnostics.Contracts;

namespace Tacny
{
    public class Main
    {
        /// <summary>
        /// Print the source code
        /// </summary>
        /// <param name="program"></param>
        /// <param name="filename"></param>
        public static void MaybePrintProgram(Dafny.Program program, string filename)
        {
            if (filename != null)
            {
                TextWriter tw;
                if (filename == "-")
                {
                    tw = System.Console.Out;
                }
                else
                {
                    tw = new System.IO.StreamWriter(filename);
                }
                Printer pr = new Printer(tw, DafnyOptions.O.PrintMode);
                pr.PrintProgram(program);
            }
        }

        /// <summary>
        /// Returns null on success, or an error string otherwise.
        /// </summary>
        public static string ParseCheck(IList<string/*!*/>/*!*/ fileNames, string/*!*/ programName, out Dafny.Program program)
        //modifies Bpl.CommandLineOptions.Clo.XmlSink.*;
        {
            Contract.Requires(programName != null);
            Contract.Requires(fileNames != null);
            program = null;
            ModuleDecl module = new Dafny.LiteralModuleDecl(new Dafny.DefaultModuleDecl(), null);
            BuiltIns builtIns = new Dafny.BuiltIns();
            foreach (string dafnyFileName in fileNames)
            {
                Contract.Assert(dafnyFileName != null);
                if (Bpl.CommandLineOptions.Clo.XmlSink != null && Bpl.CommandLineOptions.Clo.XmlSink.IsOpen)
                {
                    Bpl.CommandLineOptions.Clo.XmlSink.WriteFileFragment(dafnyFileName);
                }
                if (Bpl.CommandLineOptions.Clo.Trace)
                {
                    Console.WriteLine("Parsing " + dafnyFileName);
                }

                string err = ParseFile(dafnyFileName, Bpl.Token.NoToken, module, builtIns, new Dafny.Errors());
                if (err != null)
                {
                    return err;
                }
            }

            if (!DafnyOptions.O.DisallowIncludes)
            {
                string errString = ParseIncludes(module, builtIns, fileNames, new Dafny.Errors());
                if (errString != null)
                {
                    return errString;
                }
            }

            program = new Dafny.Program(programName, module, builtIns);

            MaybePrintProgram(program, DafnyOptions.O.DafnyPrintFile);

            if (Bpl.CommandLineOptions.Clo.NoResolve || Bpl.CommandLineOptions.Clo.NoTypecheck) { return null; }

            return null;
        }

        public static string ResolveProgram(Dafny.Program program)
        {

            Dafny.Resolver r = new Dafny.Resolver(program);
            r.ResolveProgram(program);
            MaybePrintProgram(program, DafnyOptions.O.DafnyPrintResolvedFile);

            if (r.ErrorCount != 0)
            {
                return string.Format("{0} resolution/type errors detected in {1}", r.ErrorCount, program.Name);
            }
            return null;
        }


        /// <summary>
        /// Applies tactics in the program
        /// </summary>
        /// <param name="dafnyProgram"></param>
        /// <param name="fileNames"></param>
        /// <param name="programId"></param>
        /// <param name="stats"></param>
        /// <returns></returns        
        public static string ResolveTactics(ref Dafny.Program dafnyProgram, IList<string> fileNames, string programId, out Bpl.PipelineStatistics stats)
        {
            Dafny.Program dfy_backup = dafnyProgram;
            Interpreter r = new Interpreter(dfy_backup, fileNames, programId);
            //ParseCheck(fileNames, "main_program_id", out dfy_backup);
            Bpl.Program boogieProgram = null;


            stats = null;
            // If the program does not have tactics, run the standard translation/validation and exit
            PipelineOutcome po;
            if (r.HasTactics() && TacnyOptions.O.ResolveTactics)
            {
                String err = r.ResolveProgram(ref dfy_backup);
                if (err != null)
                    return err;

                MaybePrintProgram(dfy_backup, DafnyOptions.O.DafnyPrintResolvedFile);
            }
            else
            {
                Translate(dfy_backup, fileNames, programId, out boogieProgram);
                po = BoogiePipeline(boogieProgram, dafnyProgram, fileNames, programId, out stats);
            }


            dafnyProgram = dfy_backup;

            
            // Everything is ok
            return null;
        }


        /// <summary>
        /// Translates Dafny program to boogie program
        /// </summary>
        /// <returns>Exit value</returns>
        public static void Translate(Dafny.Program dafnyProgram, IList<string> fileNames, string programId, out Bpl.Program boogieProgram)
        {

            Dafny.Translator translator = new Dafny.Translator();
            boogieProgram = translator.Translate(dafnyProgram);
            //if (CommandLineOptions.Clo.PrintFile != null)
            //{
            //    ExecutionEngine.PrintBplFile(CommandLineOptions.Clo.PrintFile, boogieProgram, false, false, CommandLineOptions.Clo.PrettyPrint);
            //}

        }

        // temp method
        /// <summary>
        /// Pipeline the boogie program to Dafny where it is valid
        /// </summary>
        /// <returns>Exit value</returns>
        public static Bpl.PipelineOutcome BoogiePipeline(Bpl.Program boogieProgram, Dafny.Program dafnyProgram, IList<string> fileNames, string programId, out Bpl.PipelineStatistics stats)
        {

            string bplFilename;
            if (CommandLineOptions.Clo.PrintFile != null)
            {
                bplFilename = CommandLineOptions.Clo.PrintFile;
            }
            else
            {
                string baseName = tcce.NonNull(Path.GetFileName(fileNames[fileNames.Count - 1]));
                baseName = tcce.NonNull(Path.ChangeExtension(baseName, "bpl"));
                bplFilename = Path.Combine(Path.GetTempPath(), baseName);
            }


            Bpl.PipelineOutcome oc = BoogiePipelineWithRerun(boogieProgram, bplFilename, out stats, 1 < Dafny.DafnyOptions.Clo.VerifySnapshots ? programId : null);

            //var allOk = stats.ErrorCount == 0 && stats.InconclusiveCount == 0 && stats.TimeoutCount == 0 && stats.OutOfMemoryCount == 0;
            return oc;
        }

        /// <summary>
        /// Resolve, type check, infer invariants for, and verify the given Boogie program.
        /// The intention is that this Boogie program has been produced by translation from something
        /// else.  Hence, any resolution errors and type checking errors are due to errors in
        /// the translation.
        /// The method prints errors for resolution and type checking errors, but still returns
        /// their error code.
        /// </summary>
        public static PipelineOutcome BoogiePipelineWithRerun(Bpl.Program/*!*/ program, string/*!*/ bplFileName,
            out PipelineStatistics stats, string programId)
        {
            Contract.Requires(program != null);
            Contract.Requires(bplFileName != null);
            Contract.Ensures(0 <= Contract.ValueAtReturn(out stats).InconclusiveCount && 0 <= Contract.ValueAtReturn(out stats).TimeoutCount);

            stats = new PipelineStatistics();
            LinearTypeChecker ltc;
            MoverTypeChecker mtc;
            PipelineOutcome oc = ExecutionEngine.ResolveAndTypecheck(program, bplFileName, out ltc, out mtc);
            switch (oc)
            {
                case PipelineOutcome.Done:
                    return oc;

                case PipelineOutcome.ResolutionError:
                case PipelineOutcome.TypeCheckingError:
                    {
                        ExecutionEngine.PrintBplFile(bplFileName, program, false, false, CommandLineOptions.Clo.PrettyPrint);
                        Console.WriteLine();
                        Console.WriteLine("*** Encountered internal translation error - re-running Boogie to get better debug information");
                        Console.WriteLine();

                        List<string/*!*/>/*!*/ fileNames = new List<string/*!*/>();
                        fileNames.Add(bplFileName);
                        Bpl.Program reparsedProgram = ExecutionEngine.ParseBoogieProgram(fileNames, true);
                        if (reparsedProgram != null)
                        {
                            ExecutionEngine.ResolveAndTypecheck(reparsedProgram, bplFileName, out ltc, out mtc);
                        }
                    }
                    return oc;

                case PipelineOutcome.ResolvedAndTypeChecked:
                    ExecutionEngine.EliminateDeadVariables(program);
                    ExecutionEngine.CollectModSets(program);
                    ExecutionEngine.CoalesceBlocks(program);
                    ExecutionEngine.Inline(program);
                    //return ExecutionEngine.InferAndVerify(program, stats, programId);
                    return ExecutionEngine.InferAndVerify(program, stats, programId, errorInfo =>
                    {
                        errorInfo.BoogieErrorCode = null;
                        Console.WriteLine(errorInfo.FullMsg);
                        //errorListHolder.AddError(new DafnyError(errorInfo.Tok.filename, errorInfo.Tok.line - 1, errorInfo.Tok.col - 1, ErrorCategory.VerificationError, errorInfo.FullMsg, s, isRecycled, errorInfo.Model.ToString(), System.IO.Path.GetFullPath(_document.FilePath) == errorInfo.Tok.filename), errorInfo.ImplementationName, requestId);
                        //foreach (var aux in errorInfo.Aux)
                        //{
                        //  errorListHolder.AddError(new DafnyError(aux.Tok.filename, aux.Tok.line - 1, aux.Tok.col - 1, ErrorCategory.AuxInformation, aux.FullMsg, s, isRecycled, null, System.IO.Path.GetFullPath(_document.FilePath) == aux.Tok.filename), errorInfo.ImplementationName, requestId);
                        //}


                    });

                default:
                    Contract.Assert(false); throw new cce.UnreachableException();  // unexpected outcome
            }
        }

        // Lower-case file names before comparing them, since Windows uses case-insensitive file names
        private class IncludeComparer : IComparer<Include>
        {
            public int Compare(Include x, Include y)
            {
                return x.fullPath.ToLower().CompareTo(y.fullPath.ToLower());
            }
        }

        public static string ParseIncludes(ModuleDecl module, BuiltIns builtIns, IList<string> excludeFiles, Dafny.Errors errs)
        {
            SortedSet<Include> includes = new SortedSet<Include>(new IncludeComparer());
            foreach (string fileName in excludeFiles)
            {
                includes.Add(new Include(null, fileName, Path.GetFullPath(fileName)));
            }
            bool newlyIncluded;
            do
            {
                newlyIncluded = false;

                List<Include> newFilesToInclude = new List<Include>();
                foreach (Include include in ((LiteralModuleDecl)module).ModuleDef.Includes)
                {
                    bool isNew = includes.Add(include);
                    if (isNew)
                    {
                        newlyIncluded = true;
                        newFilesToInclude.Add(include);
                    }
                }

                foreach (Include include in newFilesToInclude)
                {
                    string ret = ParseFile(include.filename, include.tok, module, builtIns, errs, false);
                    if (ret != null)
                    {
                        return ret;
                    }
                }
            } while (newlyIncluded);

            return null; // Success
        }

        private static string ParseFile(string dafnyFileName, Bpl.IToken tok, ModuleDecl module, BuiltIns builtIns, Dafny.Errors errs, bool verifyThisFile = true)
        {
            var fn = DafnyOptions.Clo.UseBaseNameForFileName ? Path.GetFileName(dafnyFileName) : dafnyFileName;
            try
            {
                int errorCount = Dafny.Parser.Parse(dafnyFileName, module, builtIns, errs, verifyThisFile);
                if (errorCount != 0)
                {
                    return string.Format("{0} parse errors detected in {1}", errorCount, fn);
                }
            }
            catch (IOException e)
            {
                errs.SemErr(tok, "Unable to open included file");
                return string.Format("Error opening file \"{0}\": {1}", fn, e.Message);
            }
            return null; // Success
        }
    }
}
