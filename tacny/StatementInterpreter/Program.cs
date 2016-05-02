using System;
using System.Collections.Generic;
using System.IO;
using System.CodeDom.Compiler;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Linq;

namespace Tacny
{
    public class Program
    {
        const bool DEBUG = true;
        private IList<string> fileNames;
        private string _programId;
        public string programId
        {
            set
            { _programId = value; }
            get { return _programId; }
        }
        private readonly Dafny.Program _original;
        private Dafny.Program _program;
        public Dafny.Program dafnyProgram
        {
            set
            {
                resolved = false;
                _program = value;
                errorInfo = null;
                stats = null;
            }
            get
            {
                return _program;
            }

        }
        public Bpl.ErrorInformation errorInfo;
        public List<Bpl.ErrorInformation> errList;
        public PipelineOutcome po;
        public Bpl.PipelineStatistics stats;
        public bool resolved = false;

        public readonly Dictionary<string, Tactic> tactics;
        public readonly Dictionary<string, MemberDecl> members;
        public readonly List<DatatypeDecl> globals;
        public DebugData currentDebug;
        public List<DebugData> debugDataList;

        public class DebugData
        {
            public string tactic = null;
            public string method = null;
            public int BadBranchCount = 0;      // number of branches where resolution failed
            public int GoodBranchCount = 0;     // number of branches where resolution succeeded
            public int VerificationFailure = 0; // number of times verification failed
            public int VerificationSucc = 0;    // number of times verificaiton succeeded
            public int TotalBranchCount = 0;    // total number of branches
            public int CallsToBoogie = 0;       // number of calls made to Boogie during tactic resolution
            public int CallsToDafny = 0;        // number of calls to Dafny resolver
            public int StartTime = 0;           // Unix timestamp when the tactic resolution begins
            public int EndTime = 0;             // Unix timestamp when the tactic resolution finishes
            public int TimeAtBoogie = 0;
            public int TimeAtDafny = 0;
            private bool PrintHeader = true;
          
            public DebugData(string tactic, string method)
            {
                StartTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                this.tactic = tactic;
                this.method = method;
            }

            public void Fin()
            {
                if (EndTime == 0)
                    EndTime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            }
            public void PrintDebugData()
            {
                Fin();
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                builder.AppendLine(string.Format("Method: {0}", method));
                builder.AppendLine(string.Format("Tactic: {0}", tactic));
                builder.AppendLine(string.Format("Execution time: {0} seconds", EndTime - StartTime));
                builder.AppendLine(string.Format("Generated branches: {0}", TotalBranchCount));
                builder.AppendLine(string.Format("Generated invalid branches: {0}", BadBranchCount));
                builder.AppendLine(string.Format("Generated valid branches: {0}", GoodBranchCount));
                builder.AppendLine(string.Format("Verification failed {0} times", VerificationFailure));
                builder.AppendLine(string.Format("Verification succeeded {0} times", VerificationSucc));
                builder.AppendLine(string.Format("Times Boogie was called: {0}", CallsToBoogie));
                builder.AppendLine(string.Format("Total time at Boogie: {0}", TimeAtBoogie));
                builder.AppendLine(string.Format("Times Dafny was called: {0}", CallsToDafny));
                builder.AppendLine(string.Format("Total time at Dafny: {0}", CallsToDafny));
                Util.Printer.P.PrintDebugMessage(builder.ToString());
            }

            public void PrintCsvDebugData(bool printHeader)
            {
                Fin();
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                if (printHeader)
                {
                    builder.AppendLine("Tactic, Method, Execution time, Generated Nodes, Invalid nodes, Valid nodes, Verification Failure, Verification success, Boogie calls, Boogie Wait, Dafny Calls, Dafny Wait");
                }
                builder.AppendLine(string.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}",
                    tactic, method,
                    EndTime - StartTime, TotalBranchCount,
                    BadBranchCount, GoodBranchCount,
                    VerificationFailure, VerificationSucc,
                    CallsToBoogie, TimeAtBoogie, CallsToDafny, TimeAtDafny));
                Util.Printer.P.PrintCsvData(builder.ToString());
            }
        }

        public void PrintAllDebugData(bool isCsv = false)
        {
            bool printHeader = true;
            foreach (var item in debugDataList)
            {
                if (isCsv)
                {
                    item.PrintCsvDebugData(printHeader);
                    printHeader = false;
                }
                else
                {
                    item.PrintDebugData();
                }
            }
        }


        public void PrintDebugData(DebugData debugData, bool isCsv = false)
        {
            if (isCsv)
                debugData.PrintCsvDebugData(true);
            else
                debugData.PrintDebugData();
        }


        private void IncBadBranchCount(DebugData debugData)
        {
            debugData.BadBranchCount++;
        }

        private void IncGoodBranchCount(DebugData debugData)
        {
            debugData.GoodBranchCount++;
        }

        public void IncTotalBranchCount(DebugData debugData)
        {
            debugData.TotalBranchCount++;
        }

        private void IncVerificationFailure(DebugData debugData)
        {
            debugData.VerificationFailure++;
        }

        private void IncVerificationSuccess(DebugData debugData)
        {
            debugData.VerificationSucc++;
        }

        private void IncCallsToBoogie(DebugData debugData)
        {
            debugData.CallsToBoogie++;
        }

        private void IncCallsToDafny(DebugData debugData)
        {
            debugData.CallsToDafny++;
        }

        private void IncTimeAtBoogie(DebugData debugData, int time)
        {
            debugData.TimeAtBoogie += time;
        }

        private void IncTimeAtDafny(DebugData debugData, int time)
        {
            debugData.TimeAtDafny += time;
        }

        public void SetCurrent(Tactic tac, MemberDecl md)
        {
            Contract.Requires(tac != null);
            Contract.Requires(md != null);
            DebugData dd = debugDataList.Where(i => i.tactic == tac.Name && i.method == md.Name).LastOrDefault();
            if (dd == null)
            {
                dd = new DebugData(tac.Name, md.Name);
                debugDataList.Add(dd);
            }
            currentDebug = dd;
        }

        public Program(IList<string> fileNames, string programId)
        {
            Contract.Requires(fileNames != null);
            Contract.Requires(programId != null);


            this.fileNames = fileNames;
            this.programId = programId;
            string err = ParseCheck(fileNames, programId, out _original);
            if (err != null)
                throw new Exception(err);
            dafnyProgram = ParseProgram();
            if (err != null)
                throw new ArgumentException(err);
            dafnyProgram = ParseProgram();

            Init(out tactics, out members, out globals);
        }

        public Program(IList<string> fileNames, string programId, Dafny.Program program)
        {
            this.fileNames = fileNames;
            this.programId = programId;
            this._original = program;
            this.dafnyProgram = program;
            Init(out tactics, out members, out globals);
        }

        private void Init(out Dictionary<string, Tactic> tactics, out Dictionary<string, MemberDecl> members, out List<DatatypeDecl> globals)
        {
            tactics = new Dictionary<string, Tactic>();
            members = new Dictionary<string, MemberDecl>();
            globals = new List<DatatypeDecl>();
            debugDataList = new List<DebugData>();

            foreach (var item in dafnyProgram.DefaultModuleDef.TopLevelDecls)
            {
                ClassDecl curDecl = item as ClassDecl;
                if (curDecl != null)
                {
                    foreach (var member in curDecl.Members)
                    {
                        Tactic tac = member as Tactic;
                        if (tac != null)
                            tactics.Add(tac.Name, tac);
                        else
                            members.Add(member.Name, member);
                    }
                }
                else
                {
                    DatatypeDecl dd = item as DatatypeDecl;
                    if (dd != null)
                        globals.Add(dd);
                }
            }
        }

        /// <summary>
        /// Create new tacny Program instance
        /// </summary>
        /// <returns></returns>
        public Program NewProgram()
        {
            return new Program(fileNames, programId, _original);

        }

        /// <summary>
        /// Create new instance of the working Dafny.Program
        /// </summary>
        /// <returns>Instance of dafny.Program</returns>
        public Dafny.Program ParseProgram()
        {
            //Dafny.Program prog;
            //ParseCheck(fileNames, programId, out prog);
            Cloner cl = new Cloner();
            ModuleDecl module = new Dafny.LiteralModuleDecl(cl.CloneModuleDefinition(_original.DefaultModuleDef, _original.Name), null);

            this.dafnyProgram = new Dafny.Program(_original.Name, module, _original.BuiltIns);
            return dafnyProgram;
        }

        public void VerifyProgram()
        {
            if (!resolved)
                ResolveProgram();
            VerifyProgram(dafnyProgram);
        }

        public void VerifyProgram(Dafny.Program prog)
        {
            Debug.WriteLine("Verifying Dafny program");

            IncCallsToBoogie(currentDebug);
            //disable Dafny output
            var cOut = Console.Out;
            Console.SetOut(TextWriter.Null);
            var start = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            po = Pipeline.VerifyProgram(prog, fileNames, programId, out stats, out errList, out errorInfo);
            var end = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Console.SetOut(cOut);
            IncTimeAtBoogie(currentDebug, end - start);
            if (stats.ErrorCount == 0)
            {
                Debug.WriteLine("Dafny program VERIFIED");
                IncVerificationSuccess(currentDebug);
            }
            else
            {
                Debug.WriteLine("Dafny program NOT VERIFIED");
                IncVerificationFailure(currentDebug);
            }

        }

        public bool ResolveProgram()
        {
            if (ResolveProgram(dafnyProgram) == 0)
                resolved = true;
            return resolved;
        }

        public int ResolveProgram(Dafny.Program program)
        {
            Debug.WriteLine("Resolving Dafny program");

            IncCallsToDafny(currentDebug);
            //disable Dafny output
            var cOut = Console.Out;
            Console.SetOut(TextWriter.Null);
            Dafny.Resolver r = new Dafny.Resolver(program);
            var start = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            r.ResolveProgram(program);
            var end = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Console.SetOut(cOut);
            IncTimeAtDafny(currentDebug, end - start);
            if (r.ErrorCount != 0)
            {
                Debug.WriteLine("Resolution FAILED");
                Debug.WriteLine("{0} resolution/type errors detected in {1}", r.ErrorCount, program.Name);
                IncBadBranchCount(currentDebug);
            }
            else
            {
                Debug.WriteLine("Resolution SUCCESSFUL");
                IncGoodBranchCount(currentDebug);
            }

            return r.ErrorCount;
        }

        public static MemberDecl FindMember(Dafny.Program program, string name)
        {
            foreach (var item in program.DefaultModuleDef.TopLevelDecls)
            {
                ClassDecl cd = item as ClassDecl;
                if (cd != null)
                    foreach (var member in cd.Members)
                        if (member.Name == name)
                            return member;
            }

            return null;
        }

        public List<DatatypeDecl> GetGlobals(Dafny.Program prog)
        {
            List<DatatypeDecl> data = new List<DatatypeDecl>();
            foreach (TopLevelDecl d in prog.DefaultModuleDef.TopLevelDecls)
            {
                DatatypeDecl dd = d as DatatypeDecl;
                if (dd != null)
                    data.Add(dd);
            }

            return data;
        }

        public Token GetErrorToken()
        {
            if (errorInfo != null)
                return (Token)errorInfo.Tok;

            return null;
        }

        public bool HasError()
        {
            if (stats != null)
                return stats.ErrorCount > 0;

            return true;
        }

        public void ClearBody(MemberDecl md)
        {
            ClearBody(md, dafnyProgram);
        }

        /// <summary>
        /// Remove unresolved tactic calls from the program
        /// </summary>
        /// <param name="program">Dafny Program</param>
        public void ClearBody(MemberDecl md, Dafny.Program program)
        {
            foreach (var item in program.DefaultModuleDef.TopLevelDecls)
            {
                ClassDecl cd = item as ClassDecl;
                if (cd != null)
                {
                    foreach (var member in cd.Members)
                    {
                        Method m = member as Method;
                        if (m != null && m.Name != md.Name)
                        {
                            m.Body = null;
                        }
                    }
                }
            }
        }

        // @TODO this could be optimised to hold the resolved data for all the program
        public List<IVariable> GetResolvedVariables(MemberDecl md)
        {
            ParseProgram();
            ClearBody(md);
            ResolveProgram();
            List<IVariable> result = null;
            foreach (var item in dafnyProgram.DefaultModuleDef.TopLevelDecls)
            {
                ClassDecl cd = item as ClassDecl;
                if (cd != null)
                {
                    foreach (var member in cd.Members)
                    {
                        Method m = member as Method;
                        if (m == null)
                            continue;
                        if (m.Name == md.Name)
                        {
                            result = new List<IVariable>();
                            foreach (var stmt in m.Body.Body)
                            {
                                VarDeclStmt vds = stmt as VarDeclStmt;
                                if (vds != null)
                                {
                                    foreach (var local in vds.Locals)
                                    {
                                        if (local.Type != null)
                                            result.Add(local);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        private string GetSignature(UpdateStmt us)
        {
            ExprRhs er = us.Rhss[0] as ExprRhs;
            if (er == null)
                return null;
            ApplySuffix asx = er.Expr as ApplySuffix;
            if (asx == null)
                return null;
            return asx.Lhs.tok.val;
        }

        public bool IsTacticCall(UpdateStmt us)
        {
            string name = GetSignature(us);
            if (name == null)
                return false;
            return tactics.ContainsKey(name);
        }

        public bool IsTacticFuntionCall(UpdateStmt us)
        {
            string name = GetSignature(us);
            if (name == null)
                return false;
            return !tactics.ContainsKey(name) ? false : tactics[name] is TacticFunction;
            
        }

        public Tactic GetTactic(string name)
        {
            if (!tactics.ContainsKey(name))
                return null;

            return tactics[name];
        }

        public Tactic GetTactic(UpdateStmt us)
        {
            string name = GetSignature(us);
            if (name == null)
                return null;

            return GetTactic(name);
        }

        public MemberDecl GetMember(string name)
        {
            Contract.Requires(name != null);
            if (!members.ContainsKey(name))
                return null;
            return members[name];
        }

        #region Parser
        /// <summary>
        /// Returns null on success, or an error string otherwise.
        /// </summary>
        public string ParseCheck(IList<string/*!*/>/*!*/ fileNames, string/*!*/ programName, out Dafny.Program program)
        //modifies Bpl.CommandLineOptions.Clo.XmlSink.*;
        {
            //Debug.WriteLine("ACTION: Parsing Dafny program");
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


            if (Bpl.CommandLineOptions.Clo.NoResolve || Bpl.CommandLineOptions.Clo.NoTypecheck) { return null; }
            Debug.WriteLine("SUCCESS: Parsing Dafny Program");
            return null;
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
        #endregion

        #region Boogie




        #endregion

        #region Compilation
        public void Compile()
        {
            //printer.WriteTrailer(stats);
            if ((DafnyOptions.O.Compile /*&& allOk*/ && CommandLineOptions.Clo.ProcsToCheck == null) || DafnyOptions.O.ForceCompile)
                Dafny.DafnyDriver.CompileDafnyProgram(dafnyProgram, fileNames[0]);
        }

        private static void CompileDafnyProgram(Dafny.Program dafnyProgram, string dafnyProgramName, TextWriter outputWriter = null)
        {
            Contract.Requires(dafnyProgram != null);

            if (outputWriter == null)
            {
                outputWriter = Console.Out;
            }

            // Compile the Dafny program into a string that contains the C# program
            StringWriter sw = new StringWriter();
            Dafny.Compiler compiler = new Dafny.Compiler(sw);
            compiler.ErrorWriter = outputWriter;
            var hasMain = compiler.HasMain(dafnyProgram);
            if (DafnyOptions.O.RunAfterCompile && !hasMain)
            {
                // do no more
                return;
            }
            compiler.Compile(dafnyProgram);
            var csharpProgram = sw.ToString();
            bool completeProgram = compiler.ErrorCount == 0;

            // blurt out the code to a file
            if (DafnyOptions.O.SpillTargetCode)
            {
                string targetFilename = Path.ChangeExtension(dafnyProgramName, "cs");
                using (TextWriter target = new StreamWriter(new FileStream(targetFilename, System.IO.FileMode.Create)))
                {
                    target.Write(csharpProgram);
                    if (completeProgram)
                    {
                        outputWriter.WriteLine("Compiled program written to {0}", targetFilename);
                    }
                    else
                    {
                        outputWriter.WriteLine("File {0} contains the partially compiled program", targetFilename);
                    }
                }
            }

            // compile the program into an assembly
            if (!completeProgram)
            {
                // don't compile
            }
            else if (!CodeDomProvider.IsDefinedLanguage("CSharp"))
            {
                outputWriter.WriteLine("Error: cannot compile, because there is no provider configured for input language CSharp");
            }
            else
            {
                var provider = CodeDomProvider.CreateProvider("CSharp");
                var cp = new System.CodeDom.Compiler.CompilerParameters();
                cp.GenerateExecutable = hasMain;
                if (DafnyOptions.O.RunAfterCompile)
                {
                    cp.GenerateInMemory = true;
                }
                else if (hasMain)
                {
                    cp.OutputAssembly = Path.ChangeExtension(dafnyProgramName, "exe");
                    cp.GenerateInMemory = false;
                }
                else
                {
                    cp.OutputAssembly = Path.ChangeExtension(dafnyProgramName, "dll");
                    cp.GenerateInMemory = false;
                }
                cp.CompilerOptions = "/debug /nowarn:0164 /nowarn:0219";  // warning CS0164 complains about unreferenced labels, CS0219 is about unused variables
                cp.ReferencedAssemblies.Add("System.Numerics.dll");

                var cr = provider.CompileAssemblyFromSource(cp, csharpProgram);
                var assemblyName = Path.GetFileName(cr.PathToAssembly);
                if (DafnyOptions.O.RunAfterCompile && cr.Errors.Count == 0)
                {
                    outputWriter.WriteLine("Program compiled successfully");
                    outputWriter.WriteLine("Running...");
                    outputWriter.WriteLine();
                    var entry = cr.CompiledAssembly.EntryPoint;
                    try
                    {
                        object[] parameters = entry.GetParameters().Length == 0 ? new object[] { } : new object[] { new string[0] };
                        entry.Invoke(null, parameters);
                    }
                    catch (System.Reflection.TargetInvocationException e)
                    {
                        outputWriter.WriteLine("Error: Execution resulted in exception: {0}", e.Message);
                        outputWriter.WriteLine(e.InnerException.ToString());
                    }
                    catch (Exception e)
                    {
                        outputWriter.WriteLine("Error: Execution resulted in exception: {0}", e.Message);
                        outputWriter.WriteLine(e.ToString());
                    }
                }
                else if (cr.Errors.Count == 0)
                {
                    outputWriter.WriteLine("Compiled assembly into {0}", assemblyName);
                }
                else
                {
                    outputWriter.WriteLine("Errors compiling program into {0}", assemblyName);
                    foreach (var ce in cr.Errors)
                    {
                        outputWriter.WriteLine(ce.ToString());
                        outputWriter.WriteLine();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Print verified program to file
        /// </summary>
        public void PrintProgram(Dafny.Program program = null)
        {
            if (program == null)
                Util.Printer.P.PrintProgram(dafnyProgram);
            else
                Util.Printer.P.PrintProgram(program);

        }

        public void MaybePrintProgram(string filename)
        {
            MaybePrintProgram(dafnyProgram, filename);
        }
        /// <summary>
        /// Print the source code
        /// </summary>
        /// <param name="prog"></param>
        /// <param name="filename"></param>
        public void MaybePrintProgram(Dafny.Program prog, string filename)
        {
            // if program is not in debug mode disable console printing
           // PrintProgram(prog);
        }

        public void PrintMember(Dafny.Program prog, string memberName, string filename = null)
        {
            Contract.Requires(prog != null);
            Contract.Requires(memberName != null);

            bool printToConsole = true;

            if (filename == null || filename == "-")
                printToConsole = true;
            Printer p = null;
            if (printToConsole)
                p = new Dafny.Printer(Console.Out, Util.TacnyOptions.O.PrintMode);
            else
                p = Util.Printer.P;
            foreach (var tld in prog.DefaultModuleDef.TopLevelDecls)
            {
                if (tld is ClassDecl)
                {
                    ClassDecl cd = tld as ClassDecl;
                    MemberDecl md = cd.Members.FirstOrDefault(i => i.Name == memberName);
                    if (md != null)
                    {
                        p.PrintMembers(new List<MemberDecl> { md }, Debug.IndentLevel, this.fileNames[0]);
                        return;
                    }
                }
            }

        }
    }
}
