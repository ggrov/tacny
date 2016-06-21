using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using Tacny.ArrayExtensions;
using Microsoft.Dafny;
using Type = System.Type;
using Bpl = Microsoft.Boogie;

namespace Tacny {

  public static class Parser {
    #region Parser
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
