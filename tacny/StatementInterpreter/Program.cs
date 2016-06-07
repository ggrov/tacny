using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Dafny;
using Util;
using Bpl = Microsoft.Boogie;
using Printer = Util.Printer;
using Dfy = Microsoft.Dafny;

namespace LazyTacny {

  public class TopLevelClassDeclaration {
    public readonly Dictionary<string, ITactic> Tactics;
    public readonly Dictionary<string, MemberDecl> Members;


    public TopLevelClassDeclaration() {
      Tactics = new Dictionary<string, ITactic>();
      Members = new Dictionary<string, MemberDecl>();

    }
  }

  public class Program {
    public class DebugData {
      public string Tactic;
      public string Method;
      public int BadBranchCount; // number of branches where resolution failed
      public int GoodBranchCount; // number of branches where resolution succeeded
      public int VerificationFailure; // number of times verification failed
      public int VerificationSucc; // number of times verificaiton succeeded
      public int TotalBranchCount; // total number of branches
      public int CallsToBoogie; // number of calls made to Boogie during tactic resolution
      public int CallsToDafny; // number of calls to Dafny resolver
      public double StartTime; // Unix timestamp when the tactic resolution begins
      public double EndTime = -1; // Unix timestamp when the tactic resolution finishes
      public double TimeAtBoogie;
      public double TimeAtDafny;
      private bool _printHeader = true;

      public DebugData(string tactic, string method) {
        StartTime = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        Tactic = tactic;
        Method = method;
      }

      public void Fin() {
        if (EndTime < 0)
          EndTime = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
      }

      public void PrintDebugData() {
        Fin();
        var builder = new StringBuilder();
        builder.AppendLine($"Method: {Method}");
        builder.AppendLine($"Tactic: {Tactic}");
        builder.AppendLine($"Execution time: {EndTime - StartTime} ms");
        builder.AppendLine($"Generated branches: {TotalBranchCount}");
        builder.AppendLine($"Generated invalid branches: {BadBranchCount}");
        builder.AppendLine($"Generated valid branches: {GoodBranchCount}");
        builder.AppendLine($"Verification failed {VerificationFailure} times");
        builder.AppendLine($"Verification succeeded {VerificationSucc} times");
        builder.AppendLine($"Times Boogie was called: {CallsToBoogie}");
        builder.AppendLine($"Time waited for Boogie: {TimeAtBoogie} ms");
        builder.AppendLine($"Times Dafny was called: {CallsToDafny} ms");
        builder.AppendLine($"Time waited for Dafny: {CallsToDafny}ms");
        Printer.P.PrintDebugMessage(builder.ToString());
      }

      public void PrintCsvDebugData(bool printHeader) {
        Fin();
        var builder = new StringBuilder();
        if (printHeader) {
          builder.AppendLine(
            "Tactic, Method, Execution time, Generated Nodes, Invalid nodes, Valid nodes, Verification Failure, Verification success, Boogie calls, Boogie Wait, Dafny Calls, Dafny Wait");
        }
        builder.AppendLine(
          $"{Tactic}, {Method}, {EndTime - StartTime}, {TotalBranchCount}, {BadBranchCount}, {GoodBranchCount}, {VerificationFailure}, {VerificationSucc}, {CallsToBoogie}, {TimeAtBoogie}, {CallsToDafny}, {TimeAtDafny}");
        Printer.P.PrintCsvData(builder.ToString());
      }
    }


    private readonly IList<string> _fileNames;
    public string ProgramId { set; get; }

    private readonly Dfy.Program _original;
    private Dfy.Program _program;

    public Dfy.Program DafnyProgram {
      set {
        Resolved = false;
        _program = value;
        ErrorInfo = null;
        Stats = null;
      }
      get { return _program; }

    }

    public Bpl.ErrorInformation ErrorInfo;
    public List<Bpl.ErrorInformation> ErrList;
    public Bpl.PipelineOutcome Po;
    public Bpl.PipelineStatistics Stats;
    public bool Resolved;
    public List<TopLevelClassDeclaration> TopLevelClasses;
    public TopLevelClassDeclaration CurrentTopLevelClass;
    public Dictionary<string, ITactic> Tactics => CurrentTopLevelClass.Tactics;
    public Dictionary<string, MemberDecl> Members => CurrentTopLevelClass.Members;
    public List<DatatypeDecl> Globals;
    public DebugData CurrentDebug;
    public List<DebugData> DebugDataList;


    public void PrintAllDebugData(bool isCsv = false) {
      bool printHeader = true;
      foreach (var item in DebugDataList) {
        if (isCsv) {
          item.PrintCsvDebugData(printHeader);
          printHeader = false;
        } else {
          item.PrintDebugData();
        }
      }
    }


    public void PrintDebugData(DebugData debugData, bool isCsv = false) {
      if (isCsv)
        debugData.PrintCsvDebugData(true);
      else
        debugData.PrintDebugData();
    }


    private static void IncBadBranchCount(DebugData debugData) {
      if (debugData != null)
        debugData.BadBranchCount++;
    }

    private static void IncGoodBranchCount(DebugData debugData) {
      if (debugData != null)
        debugData.GoodBranchCount++;
    }

    public void IncTotalBranchCount(DebugData debugData) {
      if (debugData != null)
        debugData.TotalBranchCount++;
    }

    private static void IncVerificationFailure(DebugData debugData) {
      if (debugData != null)
        debugData.VerificationFailure++;
    }

    private static void IncVerificationSuccess(DebugData debugData) {
      if (debugData != null)
        debugData.VerificationSucc++;
    }

    private static void IncCallsToBoogie(DebugData debugData) {
      if (debugData != null)
        debugData.CallsToBoogie++;
    }

    private static void IncCallsToDafny(DebugData debugData) {
      if (debugData != null)
        debugData.CallsToDafny++;
    }

    public void IncTimeAtBoogie(DebugData debugData, double time) {
      if (debugData != null)
        debugData.TimeAtBoogie += time;
    }

    private static void IncTimeAtDafny(DebugData debugData, double time) {
      if (debugData != null)
        debugData.TimeAtDafny += time;
    }

    public void SetCurrent(ITactic tac, MemberDecl md) {
      Contract.Requires(tac != null);
      Contract.Requires(md != null);
      var dd = DebugDataList.LastOrDefault(i => i.Tactic == tac.Name && i.Method == md.Name);
      if (dd == null) {
        dd = new DebugData(tac.Name, md.Name);
        DebugDataList.Add(dd);
      }
      CurrentDebug = dd;
    }

    public Program(IList<string> fileNames, string programId) {
      Contract.Requires(fileNames != null);
      Contract.Requires(programId != null);


      _fileNames = fileNames;
      ProgramId = programId;
      string err = ParseCheck(fileNames, programId, out _original);
      if (err != null)
        throw new ArgumentException(err);
      DafnyProgram = ParseProgram();

      Init();
    }

    public Program(IList<string> fileNames, string programId, Dfy.Program program) {
      _fileNames = fileNames;
      ProgramId = programId;
      _original = program;
      DafnyProgram = program;
      Init();
    }

    private void Init() {
      DebugDataList = new List<DebugData>();
      TopLevelClasses = new List<TopLevelClassDeclaration>();
      Globals = new List<DatatypeDecl>();

      foreach (var item in DafnyProgram.DefaultModuleDef.TopLevelDecls) {

        ClassDecl curDecl = item as ClassDecl;
        if (curDecl != null) {
          var temp = new TopLevelClassDeclaration();

          foreach (var member in curDecl.Members) {
            Tactic tac = member as Tactic;
            if (tac != null)
              temp.Tactics.Add(tac.Name, tac);
            else {
              var tacFun = member as TacticFunction;
              if (tacFun != null)
                temp.Tactics.Add(tacFun.Name, tacFun);
              else
                temp.Members.Add(member.Name, member);
            }

          }
          TopLevelClasses.Add(temp);
        } else {
          DatatypeDecl dd = item as DatatypeDecl;
          if (dd != null)
            Globals.Add(dd);
        }
      }
    }

    /// <summary>
    /// Create new tacny Program instance
    /// </summary>
    /// <returns></returns>
    public Program NewProgram() {
      return new Program(_fileNames, ProgramId, _original);

    }

    /// <summary>
    /// Create new instance of the working Dafny.Program
    /// </summary>
    /// <returns>Instance of dafny.Program</returns>
    public Dfy.Program ParseProgram() {
      //Dafny.Program prog;
      //ParseCheck(fileNames, programId, out prog);
      Cloner cl = new Cloner();
      ModuleDecl module = new LiteralModuleDecl(cl.CloneModuleDefinition(_original.DefaultModuleDef, _original.Name),
        null);

      DafnyProgram = new Dfy.Program(_original.Name, module, _original.BuiltIns, new ConsoleErrorReporter());
      return DafnyProgram;
    }

    public void VerifyProgram() {
      if (!Resolved)
        ResolveProgram();
      VerifyProgram(DafnyProgram);
    }

    public void VerifyProgram(Dfy.Program prog) {
      Debug.WriteLine("Verifying Dafny program");

      IncCallsToBoogie(CurrentDebug);
      //disable Dafny output
      var cOut = Console.Out;
      //  Console.SetOut(TextWriter.Null);
      double start = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
      var pl = new Pipeline();
      Po = pl.VerifyProgram(prog, _fileNames, ProgramId, out Stats, out ErrList, out ErrorInfo);
      double end = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
      Console.SetOut(cOut);
      IncTimeAtBoogie(CurrentDebug, end - start);
      if (Stats.ErrorCount == 0) {
        Debug.WriteLine("Dafny program VERIFIED");
        IncVerificationSuccess(CurrentDebug);
      } else {
        Debug.WriteLine("Dafny program NOT VERIFIED");
        IncVerificationFailure(CurrentDebug);
      }

    }

    public bool ResolveProgram() {
      if (ResolveProgram(DafnyProgram) == 0)
        Resolved = true;
      return Resolved;
    }

    public int ResolveProgram(Dfy.Program program) {
      Debug.WriteLine("Resolving Dafny program");

      IncCallsToDafny(CurrentDebug);
      //disable Dafny output
      var cOut = Console.Out;
      //Console.SetOut(TextWriter.Null);
      Resolver r = new Resolver(program);
      var start = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
      r.ResolveProgram(program);
      var end = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
      Console.SetOut(cOut);
      IncTimeAtDafny(CurrentDebug, end - start);

      if (program.reporter.Count(ErrorLevel.Error) != 0) {
        Debug.WriteLine("Resolution FAILED");
        Debug.WriteLine("{0} resolution/type errors detected in {1}", program.reporter.Count(ErrorLevel.Error),
          program.Name);
        IncBadBranchCount(CurrentDebug);
      } else {
        Debug.WriteLine("Resolution SUCCESSFUL");
        IncGoodBranchCount(CurrentDebug);
      }

      return program.reporter.Count(ErrorLevel.Error);
    }

    public static MemberDecl FindMember(Dfy.Program program, string name) {
      return
        program.DefaultModuleDef.TopLevelDecls.OfType<ClassDecl>()
          .SelectMany(cd => cd.Members)
          .FirstOrDefault(member => member.Name == name);
    }

    public List<DatatypeDecl> GetGlobals(Dfy.Program prog) {
      return prog.DefaultModuleDef.TopLevelDecls.OfType<DatatypeDecl>().ToList();
    }

    public Bpl.IToken GetErrorToken() {
      return ErrorInfo.Tok;
    }

    public bool HasError() {
      if (Stats != null)
        return Stats.ErrorCount > 0;

      return true;
    }

    public void ClearBody(MemberDecl md) {
      ClearBody(md, DafnyProgram);
    }


    public static TopLevelDecl NewClassDecl(ClassDecl cd, MemberDecl nmd) {
      var mdl = cd.Members.Where(md => md != null && !(md is ITactic)).ToList();
      mdl.Add(nmd);
      return new ClassDecl(cd.tok, cd.Name, cd.Module, cd.TypeArgs, mdl, cd.Attributes, cd.TraitsTyp);
    }

    public static TopLevelDecl RemoveTactics(ClassDecl cd) {
      Contract.Requires(cd != null);
      var mdl = cd.Members.Where(md => !(md is ITactic)).ToList();

      return new ClassDecl(cd.tok, cd.Name, cd.Module, cd.TypeArgs, mdl, cd.Attributes, cd.TraitsTyp);
    }

    /// <summary>
    /// Remove unresolved tactic calls from the program
    /// </summary>
    /// <param name="md"></param>
    /// <param name="program">Dafny Program</param>
    public void ClearBody(MemberDecl md, Dfy.Program program) {
      foreach (var item in program.DefaultModuleDef.TopLevelDecls) {
        var cd = item as ClassDecl;
        if (cd == null) continue;
        foreach (var member in cd.Members) {
          var m = member as Method;
          if (m != null && m.Name != md.Name) {
            m.Body = null;
          }
        }
      }
    }

    public static Dfy.Program RemoveTactics(Dfy.Program program) {
      for (var i = 0; i < program.DefaultModuleDef.TopLevelDecls.Count; i++) {
        var curDecl = program.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
        if (curDecl != null) {
          program.DefaultModuleDef.TopLevelDecls[i] = RemoveTactics(curDecl);
        }
      }

      return program;
    }


    // @TODO this could be optimised to hold the resolved data for all programs
    public List<IVariable> GetResolvedVariables(MemberDecl md) {
      ParseProgram();
      ClearBody(md);
      RemoveTactics(DafnyProgram);
      ResolveProgram();
      List<IVariable> result = null;
      foreach (var item in DafnyProgram.DefaultModuleDef.TopLevelDecls) {
        var cd = item as ClassDecl;
        if (cd == null) continue;
        foreach (var member in cd.Members) {
          var m = member as Method;
          if (m == null)
            continue;
          if (m.Name != md.Name) continue;
          result = new List<IVariable>();
          foreach (var stmt in m.Body.Body) {
            var vds = stmt as VarDeclStmt;
            if (vds == null) continue;
            result.AddRange(vds.Locals.Where(local => local.Type != null));
          }
        }
      }
      return result;
    }

    public static string GetSignature(UpdateStmt us) {
      ExprRhs er = us.Rhss[0] as ExprRhs;
      if (er == null)
        return null;
      ApplySuffix asx = er.Expr as ApplySuffix;
      return GetSignature(asx);
    }

    public static string GetSignature(ApplySuffix aps) {
      return aps?.Lhs.tok.val;
    }

    public bool IsTacticCall(UpdateStmt us) {
      return IsTacticCall(GetSignature(us));
    }

    public bool IsTacticCall(ApplySuffix aps) {
      return IsTacticCall(GetSignature(aps));
    }

    public bool IsTacticCall(string name) {
      if (name == null)
        return false;
      return Tactics.ContainsKey(name);
    }

    public bool IsTacticFuntionCall(UpdateStmt us) {
      string name = GetSignature(us);
      if (name == null)
        return false;
      return Tactics.ContainsKey(name) && Tactics[name] is TacticFunction;

    }

    public ITactic GetTactic(string name) {
      Contract.Requires(name != null);
      if (!Tactics.ContainsKey(name))
        return null;

      return Tactics[name];
    }

    public ITactic GetTactic(UpdateStmt us) {
      Contract.Requires(us != null);
      string name = GetSignature(us);
      if (name == null)
        return null;
      return GetTactic(name);
    }

    public ITactic GetTactic(ApplySuffix aps) {
      Contract.Requires(aps != null);
      return GetTactic(GetSignature(aps));
    }

    public MemberDecl GetMember(string name) {
      Contract.Requires(name != null);
      if (!Members.ContainsKey(name))
        return null;
      return Members[name];
    }

    #region Parser

    /// <summary>
    /// Returns null on success, or an error string otherwise.
    /// </summary>
    public string ParseCheck(IList<string /*!*/> /*!*/ fileNames, string /*!*/ programName, out Dfy.Program program)
    //modifies Bpl.CommandLineOptions.Clo.XmlSink.*;
    {
      //Debug.WriteLine("ACTION: Parsing Dafny program");
      Contract.Requires(programName != null);
      Contract.Requires(fileNames != null);
      program = null;
      ModuleDecl module = new LiteralModuleDecl(new DefaultModuleDecl(), null);
      BuiltIns builtIns = new BuiltIns();
      foreach (string dafnyFileName in fileNames) {
        Contract.Assert(dafnyFileName != null);
        if (Bpl.CommandLineOptions.Clo.XmlSink != null && Bpl.CommandLineOptions.Clo.XmlSink.IsOpen) {
          Bpl.CommandLineOptions.Clo.XmlSink.WriteFileFragment(dafnyFileName);
        }
        if (Bpl.CommandLineOptions.Clo.Trace) {
          Console.WriteLine("Parsing " + dafnyFileName);
        }

        string err = ParseFile(dafnyFileName, Bpl.Token.NoToken, module, builtIns,
          new Errors(new ConsoleErrorReporter()));
        if (err != null) {
          return err;
        }
      }

      if (!DafnyOptions.O.DisallowIncludes) {
        string errString = ParseIncludes(module, builtIns, fileNames, new Errors(new ConsoleErrorReporter()));
        if (errString != null) {
          return errString;
        }
      }

      program = new Dfy.Program(programName, module, builtIns, new ConsoleErrorReporter());


      if (Bpl.CommandLineOptions.Clo.NoResolve || Bpl.CommandLineOptions.Clo.NoTypecheck) {
        return null;
      }
      Debug.WriteLine("SUCCESS: Parsing Dafny Program");
      return null;
    }

    // Lower-case file names before comparing them, since Windows uses case-insensitive file names
    private class IncludeComparer : IComparer<Include> {
      public int Compare(Include x, Include y) {
        return string.Compare(x.fullPath.ToLower(), y.fullPath.ToLower(), StringComparison.Ordinal);
      }
    }

    public static string ParseIncludes(ModuleDecl module, BuiltIns builtIns, IList<string> excludeFiles, Errors errs) {
      SortedSet<Include> includes = new SortedSet<Include>(new IncludeComparer());
      foreach (string fileName in excludeFiles) {
        includes.Add(new Include(null, fileName, Path.GetFullPath(fileName)));
      }
      bool newlyIncluded;
      do {
        newlyIncluded = false;

        var newFilesToInclude = new List<Include>();
        foreach (var include in ((LiteralModuleDecl)module).ModuleDef.Includes) {
          bool isNew = includes.Add(include);
          if (!isNew) continue;
          newlyIncluded = true;
          newFilesToInclude.Add(include);
        }

        foreach (var include in newFilesToInclude) {
          string ret = ParseFile(include.filename, include.tok, module, builtIns, errs, false);
          if (ret != null) {
            return ret;
          }
        }
      } while (newlyIncluded);

      return null; // Success
    }

    private static string ParseFile(string dafnyFileName, Bpl.IToken tok, ModuleDecl module, BuiltIns builtIns,
      Errors errs, bool verifyThisFile = true) {
      string fn = Bpl.CommandLineOptions.Clo.UseBaseNameForFileName ? Path.GetFileName(dafnyFileName) : dafnyFileName;
      try {

        int errorCount = Parser.Parse(dafnyFileName, module, builtIns, errs, verifyThisFile);
        if (errorCount != 0) {
          return $"{errorCount} parse errors detected in {fn}";
        }
      } catch (IOException e) {
        errs.SemErr(tok, "Unable to open included file");
        return $"Error opening file \"{fn}\": {e.Message}";
      }
      return null; // Success
    }

    #endregion

    /// <summary>
    /// Print verified program to file
    /// </summary>
    public void PrintProgram(Dfy.Program program = null) {
      Printer.P.PrintProgram(program ?? DafnyProgram);
    }

    public void MaybePrintProgram(string filename) {
      MaybePrintProgram(DafnyProgram, filename);
    }

    /// <summary>
    /// Print the source code
    /// </summary>
    /// <param name="prog"></param>
    /// <param name="filename"></param>
    public void MaybePrintProgram(Dfy.Program prog, string filename) {
      // if program is not in debug mode disable console printing
      // PrintProgram(prog);
    }

    public void PrintMember(Dfy.Program prog, string memberName, string filename = null) {
      Contract.Requires(prog != null);
      Contract.Requires(memberName != null);

      var printToConsole = true;

      if (filename == null || filename == "-")
        printToConsole = true;
      Dfy.Printer p = null;
      if (printToConsole)
        p = new Dfy.Printer(Console.Out, TacnyOptions.O.PrintMode);
      else
        p = Printer.P;
      foreach (var tld in prog.DefaultModuleDef.TopLevelDecls) {
        if (!(tld is ClassDecl)) continue;
        var cd = tld as ClassDecl;
        var md = cd.Members.FirstOrDefault(i => i.Name == memberName);
        if (md == null) continue;
        p.PrintMembers(new List<MemberDecl> { md }, Debug.IndentLevel, _fileNames[0]);
        return;
      }
    }

    public void PrintBoogieProgram() {
      var program = ParseProgram();
      RemoveTactics(program);
      ResolveProgram(program);
      Pipeline.PrintBoogieProgram(program);
    }

    public bool HasTacticApplications() {
      return TopLevelClasses.Any(item => item.Tactics.Count > 0);
    }


    public Dfy.Program GenerateProgram(Solution solution, Dfy.Program program = null) {
      Debug.WriteLine("Generating Dafny program");
      var ac = solution.State.Copy();
      MemberDecl newMemberDecl;
      MemberDecl oldMd = null;
      ac.Fin();
      var prog = program ?? ParseProgram();     
      for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++) {
        var curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
        if (curDecl != null) {
          if (!ac.IsFunction) {
            oldMd = Copy.CopyMember(ac.DynamicContext.md);
            var method = oldMd as Method;
            Contract.Assert(method != null, "Source method not found");

            var tacCall = ac.GetTacticCall();
            var body = method.Body.Body;
            body = Solution.InsertSolution(body, tacCall, ac.GetResolved());
            Contract.Assert(body != null, "Unable to insert the solution");

            newMemberDecl = Solution.GenerateMethod(method, body, ac.DynamicContext.newTarget as Method, FreshMemberName(method, curDecl));
          } else {
            newMemberDecl = ac.GetNewTarget();
          }
          // scan each member for tactic calls and resolve if found
          for (int j = 0; j < curDecl.Members.Count; j++) {


            if (curDecl.Members[j].Name == oldMd?.Name)
              curDecl.Members[j] = null;
          }

          prog.DefaultModuleDef.TopLevelDecls[i] = NewClassDecl(curDecl, newMemberDecl);
        }
      }

      Debug.WriteLine("Dafny program generated");

      return prog;
    }


    public string FreshMemberName(MemberDecl original, ClassDecl context) {
      int count = context.Members.Count(m => m.Name == original.Name);
      string name = $"{original.Name}_{count}";
      while (count != 0) {
        name = $"{original.Name}_{count}";
        count = context.Members.Count(m => m.Name == name);
      }

      return name;
    }
  }
}