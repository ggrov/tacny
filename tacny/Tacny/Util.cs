using System;
using System.Collections.Generic;
using System.Reflection;
using Tacny.ArrayExtensions;
using Microsoft.Dafny;
using Type = System.Type;


namespace Tacny {

  public static class ProgramGenerator {

    //public static ErrorReporter reporter;
    //public static Program GenerateProgram(Program prog, ProofState state) {
      
    //  var ac = state.Copy();
    //  MemberDecl newMemberDecl;
    //  if (!ac.IsFunction) {
    //    var method = Tacny.Program.FindMember(prog, ac.DynamicContext.md.Name) as Method;
    //    if (method == null)
    //      throw new Exception("Method not found");
    //    UpdateStmt tacCall = ac.GetTacticCall();
    //    List<Statement> body = method.Body.Body;
    //    body = InsertSolution(body, tacCall, ac.GetResolved());
    //    if (body == null)
    //      return null;
    //    if (!isFinal) {
    //      for (int i = 0; i < body.Count; i++) {
    //        var us = body[i] as UpdateStmt;
    //        if (us == null) continue;
    //        if (State.StaticContext.program.IsTacticCall(us))
    //          body.RemoveAt(i);
    //      }
    //    }

    //    newMemberDecl = GenerateMethod(method, body, ac.DynamicContext.newTarget as Method);
    //  } else {
    //    newMemberDecl = ac.GetNewTarget();
    //  }
    //  for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++) {
    //    var curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
    //    if (curDecl != null) {
    //      // scan each member for tactic calls and resolve if found
    //      for (int j = 0; j < curDecl.Members.Count; j++) {


    //        if (curDecl.Members[j].Name == newMemberDecl.Name)
    //          curDecl.Members[j] = newMemberDecl;
    //      }

    //      prog.DefaultModuleDef.TopLevelDecls[i] = Tacny.Program.RemoveTactics(curDecl);
    //    }
    //  }

    //  Debug.WriteLine("Dafny program generated");
    //  return null;
    //}

    //private static Method GenerateMethod(Method oldMd, List<Statement> body, Method source = null) {
    //  var src = source ?? oldMd;
    //  var mdBody = new BlockStmt(src.Body.Tok, src.Body.EndTok, body);
    //  var type = src.GetType();
    //  if (type == typeof(Lemma))
    //    return new Lemma(src.tok, src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
    //    src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
    //  if (type == typeof(CoLemma))
    //    return new CoLemma(src.tok, src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
    //      src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
    //  return new Method(src.tok, src.Name, src.HasStaticKeyword, src.IsGhost,
    //    src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod, src.Ens, src.Decreases,
    //    mdBody, src.Attributes, src.SignatureEllipsis);
    //}


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
