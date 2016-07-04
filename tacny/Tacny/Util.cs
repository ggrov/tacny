using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using Tacny.ArrayExtensions;
using Microsoft.Dafny;
using Type = System.Type;
using Bpl = Microsoft.Boogie;

namespace Tacny {

  public static class Util {

    public static Expression VariableToExpression(IVariable variable) {
      Contract.Requires(variable != null);
      return new NameSegment(variable.Tok, variable.Name, null);
    }

    public static NameSegment GetNameSegment(UpdateStmt us) {
      Contract.Requires(us != null);
      var rhs = us.Rhss[0] as ExprRhs;
      return rhs == null ? null : GetNameSegment(rhs.Expr as ApplySuffix);
    }

    [Pure]
    public static NameSegment GetNameSegment(ApplySuffix aps) {
      Contract.Requires<ArgumentNullException>(aps != null, "aps");
      var lhs = aps.Lhs as ExprDotName;
      if (lhs == null) return aps?.Lhs as NameSegment;
      var edn = lhs;
      return edn.Lhs as NameSegment;
    }

    /// <summary>
    ///   Return the string signature of an UpdateStmt
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    public static string GetSignature(UpdateStmt us) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(us));
      Contract.Ensures(Contract.Result<string>() != null);
      var er = us.Rhss[0] as ExprRhs;
      Contract.Assert(er != null);
      return GetSignature(er.Expr as ApplySuffix);
    }

    /// <summary>
    ///   Return the string signature of an ApplySuffix
    /// </summary>
    /// <param name="aps"></param>
    /// <returns></returns>
    public static string GetSignature(ApplySuffix aps) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(aps));
      Contract.Ensures(Contract.Result<string>() != null);
      return aps?.Lhs.tok.val;
    }

    /// <summary>
    /// Insert generated code into a method
    /// </summary>
    /// <param name="state"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    public static BlockStmt InsertCode(ProofState state, Dictionary<UpdateStmt, List<Statement>> code) {
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentNullException>(code != null, "code");
      var prog = state.GetDafnyProgram();
      var tld = prog.DefaultModuleDef.TopLevelDecls.FirstOrDefault(x => x.Name == state.ActiveClass.Name) as ClassDecl;
      Contract.Assert(tld != null);
      var member = tld.Members.FirstOrDefault(x => x.Name == state.TargetMethod.Name) as Method;
      var body = member?.Body;

      foreach (var kvp in code) {
        body = InsertCodeInternal(body, kvp.Value, kvp.Key);
      }
      var r = new Resolver(prog);
      r.ResolveProgram(prog);
      return body;
    }

    private static BlockStmt InsertCodeInternal(BlockStmt body, List<Statement> code, UpdateStmt tacticCall) {
      Contract.Requires<ArgumentNullException>(body != null, "body ");
      Contract.Requires<ArgumentNullException>(tacticCall != null, "'tacticCall");

      for (var i = 0; i < body.Body.Count; i++) {
        var stmt = body.Body[i];
        if (stmt is UpdateStmt) {
          // compare tokens
          if (Compare(tacticCall.Tok, stmt.Tok)) {
            body.Body.RemoveAt(i);
            body.Body.InsertRange(i, code);
            return body;
          }
        } else if (stmt is IfStmt) {
          body.Body[i] = InsertCodeIfStmt((IfStmt)stmt, code, tacticCall);
        } else if (stmt is WhileStmt) {
          ((WhileStmt)stmt).Body = InsertCodeInternal(((WhileStmt)stmt).Body, code, tacticCall);
        } else if (stmt is MatchStmt) {
          //TODO:
        } else if (stmt is CalcStmt) {
          //TODO:
        }
      }
      return body;
    }

    private static IfStmt InsertCodeIfStmt(IfStmt stmt, List<Statement> code, UpdateStmt tacticCall) {
      Contract.Requires<ArgumentNullException>(stmt != null, "stmt");
      Contract.Requires<ArgumentNullException>(code != null, "code");
      Contract.Requires<ArgumentNullException>(tacticCall != null, "tacticCall");

      stmt.Thn = InsertCodeInternal(stmt.Thn, code, tacticCall);
      if (stmt.Els is BlockStmt) {
        stmt.Els = InsertCodeInternal((BlockStmt)stmt.Els, code, tacticCall);
      } else if (stmt.Els is IfStmt) {
        stmt.Els = InsertCodeIfStmt((IfStmt)stmt.Els, code, tacticCall);
      }
      return stmt;
    }

    public static bool Compare(Bpl.IToken a, Bpl.IToken b) {
      Contract.Requires<ArgumentNullException>(a != null, "a");
      Contract.Requires<ArgumentNullException>(b != null, "b");
      return a.col == b.col && a.line == b.line && a.filename == b.filename;
    }



    public static Dictionary<ProofState, MemberDecl> GenerateMembers(ProofState state, Dictionary<ProofState, BlockStmt> bodies) {
      Contract.Requires<ArgumentNullException>(state != null, "state");
      Contract.Requires<ArgumentNullException>(bodies != null, "bodies");
      var result = new Dictionary<ProofState, MemberDecl>();
      var cl = new Cloner();
      foreach (var body in bodies) {
        var md = cl.CloneMember(state.TargetMethod) as Method;
        md.Body.Body.Clear();
        md.Body.Body.AddRange(body.Value.Body);
        if (result.Values.All(x => x.Name != md.Name))
          result.Add(body.Key, md);
        else {
          md = new Method(md.tok, FreshMemberName(md, result.Values.ToList()), md.HasStaticKeyword, md.IsGhost, md.TypeArgs, md.Ins,
            md.Outs, md.Req, md.Mod, md.Ens, md.Decreases, md.Body, md.Attributes, md.SignatureEllipsis);
          result.Add(body.Key, md);
        }
      }
      return result;
    }

    public static Program GenerateDafnyProgram(ProofState state, List<MemberDecl> newMembers) {
      var prog = state.GetDafnyProgram();
      var tld = prog.DefaultModuleDef.TopLevelDecls.FirstOrDefault(x => x.Name == state.TargetMethod.EnclosingClass.Name) as ClassDecl;
      Contract.Assert(tld != null);
      var member = tld.Members.FirstOrDefault(x => x.Name == state.TargetMethod.Name);
      Contract.Assert(member != null);
      int index = tld.Members.IndexOf(member);
      tld.Members.RemoveAt(index);
      // we can safely remove the tactics
      tld.Members.RemoveAll(x => x is Tactic);
      tld.Members.InsertRange(index, newMembers);
      var filePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
      var tw = new StreamWriter(filePath);
      var printer = new Printer(tw);
      printer.PrintTopLevelDecls(prog.DefaultModuleDef.TopLevelDecls, 0, filePath);
      tw.Close();
      Parser.ParseCheck(new List<string>() { filePath}, prog.Name, out prog);
      return prog;
    }

    public static string FreshMemberName(MemberDecl original, List<MemberDecl> context) {
      Contract.Requires<ArgumentNullException>(original != null, "original");
      Contract.Requires<ArgumentNullException>(context != null, "context");
      int count = context.Count(m => m.Name == original.Name);
      string name = $"{original.Name}_{count}";
      while (count != 0) {
        name = $"{original.Name}_{count}";
        count = context.Count(m => m.Name == name);
      }

      return name;
    }


    public static List<Bpl.ErrorInformation> ResolveAndVerify(Program program, Bpl.ErrorReporterDelegate er) {
      Contract.Requires<ArgumentNullException>(program != null);
      var r = new Resolver(program);
      //var start = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
      r.ResolveProgram(program);
      //var end = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds
      var boogieProg = Translate(program, program.Name);
      Bpl.PipelineStatistics stats;
      List<Bpl.ErrorInformation> errorList;
      Bpl.PipelineOutcome tmp = BoogiePipeline(boogieProg, new List<string> {program.Name}, program.Name, er, out stats, out errorList);
      return errorList;
    }

    public static Bpl.Program Translate(Program dafnyProgram, string uniqueIdPrefix) {
      Contract.Requires<ArgumentNullException>(dafnyProgram != null, "dafnyProgram");
      Contract.Requires<ArgumentNullException>(uniqueIdPrefix != null, "uniqueIdPrefix");
      Contract.Ensures(Contract.Result<Bpl.Program>() != null);
      var translator = new Translator(dafnyProgram.reporter) {
        InsertChecksums = true,
        UniqueIdPrefix = uniqueIdPrefix
      };
      return translator.Translate(dafnyProgram);
    }


    /// <summary>
    /// Pipeline the boogie program to Dafny where it is valid
    /// </summary>
    /// <returns>Exit value</returns>
    public static Bpl.PipelineOutcome BoogiePipeline(Bpl.Program program, IList<string> fileNames, string programId, Bpl.ErrorReporterDelegate er, out Bpl.PipelineStatistics stats, out List<Bpl.ErrorInformation> errorList) {
      Contract.Requires(program != null);
      Contract.Ensures(0 <= Contract.ValueAtReturn(out stats).InconclusiveCount && 0 <= Contract.ValueAtReturn(out stats).TimeoutCount);

      Bpl.LinearTypeChecker ltc;
      Bpl.CivlTypeChecker ctc;
      string baseName = cce.NonNull(Path.GetFileName(fileNames[fileNames.Count - 1]));
      baseName = cce.NonNull(Path.ChangeExtension(baseName, "bpl"));
      string bplFileName = Path.Combine(Path.GetTempPath(), baseName);

      errorList = new List<Bpl.ErrorInformation>();
      stats = new Bpl.PipelineStatistics();

    

      Bpl.PipelineOutcome oc = Bpl.ExecutionEngine.ResolveAndTypecheck(program, bplFileName, out ltc, out ctc);
      switch (oc) {
        case Bpl.PipelineOutcome.ResolvedAndTypeChecked:
          Bpl.ExecutionEngine.EliminateDeadVariables(program);
          Bpl.ExecutionEngine.CollectModSets(program);
          Bpl.ExecutionEngine.CoalesceBlocks(program);
          Bpl.ExecutionEngine.Inline(program);
          errorList = new List<Bpl.ErrorInformation>();
          var tmp = new List<Bpl.ErrorInformation>();

          oc = Bpl.ExecutionEngine.InferAndVerify(program, stats, programId, errorInfo => {
            tmp.Add(errorInfo);
            er(errorInfo);
          });
          errorList.AddRange(tmp);
          
          return oc;
        default:
          Contract.Assert(false); throw new cce.UnreachableException();  // unexpected outcome
      }
    }
  }

  #region Parser
  public static class Parser {
    /// <summary>
    /// Returns null on success, or an error string otherwise.
    /// </summary>
    public static string ParseCheck(IList<string/*!*/>/*!*/ fileNames, string/*!*/ programName, out Program program) {
      Contract.Requires(programName != null);
      Contract.Requires(fileNames != null);
      program = null;
      ModuleDecl module = new LiteralModuleDecl(new DefaultModuleDecl(), null);
      BuiltIns builtIns = new BuiltIns();
      foreach (string dafnyFileName in fileNames) {
        Contract.Assert(dafnyFileName != null);

        string err = ParseFile(dafnyFileName, Bpl.Token.NoToken, module, builtIns, new Errors(new ConsoleErrorReporter()));
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

      program = new Program(programName, module, builtIns, new ConsoleErrorReporter());
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

    private static string ParseFile(string dafnyFileName, Bpl.IToken tok, ModuleDecl module, BuiltIns builtIns, Errors errs, bool verifyThisFile = true) {
      string fn = Bpl.CommandLineOptions.Clo.UseBaseNameForFileName ? Path.GetFileName(dafnyFileName) : dafnyFileName;
      try {

        int errorCount = Microsoft.Dafny.Parser.Parse(dafnyFileName, module, builtIns, errs, verifyThisFile);
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
  }


  public static class ObjectExtensions {
    private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

    public static bool IsPrimitive(this Type type) {
      if (type == typeof(String)) return true;
      return (type.IsValueType & type.IsPrimitive);
    }

    public static object Copy(this object originalObject) {
      return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
    }
    private static object InternalCopy(Object originalObject, IDictionary<Object, Object> visited) {
      if (originalObject == null) return null;
      var typeToReflect = originalObject.GetType();
      if (IsPrimitive(typeToReflect)) return originalObject;
      if (visited.ContainsKey(originalObject)) return visited[originalObject];
      if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
      var cloneObject = CloneMethod.Invoke(originalObject, null);
      if (typeToReflect.IsArray) {
        var arrayType = typeToReflect.GetElementType();
        if (IsPrimitive(arrayType) == false) {
          Array clonedArray = (Array)cloneObject;
          clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
        }

      }
      visited.Add(originalObject, cloneObject);
      CopyFields(originalObject, visited, cloneObject, typeToReflect);
      RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
      return cloneObject;
    }

    private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect) {
      if (typeToReflect.BaseType != null) {
        RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
        CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
      }
    }

    private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null) {
      foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags)) {
        if (filter != null && filter(fieldInfo) == false) continue;
        if (IsPrimitive(fieldInfo.FieldType)) continue;
        var originalFieldValue = fieldInfo.GetValue(originalObject);
        var clonedFieldValue = InternalCopy(originalFieldValue, visited);
        fieldInfo.SetValue(cloneObject, clonedFieldValue);
      }
    }
    public static T Copy<T>(this T original) {
      return (T)Copy((Object)original);
    }
  }

  public class ReferenceEqualityComparer : EqualityComparer<Object> {
    public override bool Equals(object x, object y) {
      return ReferenceEquals(x, y);
    }
    public override int GetHashCode(object obj) {
      if (obj == null) return 0;
      return obj.GetHashCode();
    }
  }

  namespace ArrayExtensions {
    public static class ArrayExtensions {
      public static void ForEach(this Array array, Action<Array, int[]> action) {
        if (array.LongLength == 0) return;
        ArrayTraverse walker = new ArrayTraverse(array);
        do action(array, walker.Position);
        while (walker.Step());
      }
    }

    internal class ArrayTraverse {
      public int[] Position;
      private int[] maxLengths;

      public ArrayTraverse(Array array) {
        maxLengths = new int[array.Rank];
        for (int i = 0; i < array.Rank; ++i) {
          maxLengths[i] = array.GetLength(i) - 1;
        }
        Position = new int[array.Rank];
      }

      public bool Step() {
        for (int i = 0; i < Position.Length; ++i) {
          if (Position[i] < maxLengths[i]) {
            Position[i]++;
            for (int j = 0; j < i; j++) {
              Position[j] = 0;
            }
            return true;
          }
        }
        return false;
      }
    }
  }

}
