using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

/// <summary>
/// A class containing static methods to extend the functionality of Code Contracts
/// </summary>

namespace Tacny {
  public class tcce {
    [Pure]
    public static bool NonNull<T>(T t) where T : class {
      return t != null;
    }

    [Pure]
    public static bool NonNullElements<T>(IEnumerable<T> collection) where T : class {
      return collection != null && Contract.ForAll(collection, c => c != null);
    }

    [Pure]
    public static bool OfSize<T>(IList<T> collection, int size) where T : class {
      return collection != null && collection.Count == size;
    }

    [Pure]
    public static bool NonEmpty<T>(IList<T> collection) where T : class {
      return collection != null && collection.Count != 0;
    }

    [Pure]
    public static bool NonNullDictionaryAndValues<TKey, TValue>(IDictionary<TKey, TValue> collection)
      where TValue : class {
      return collection != null && NonNullElements(collection.Values);
    }

    [Pure]
    public static bool NonNullElements<T>(Graph<T> collection) where T : class {
      return collection != null && NonNullElements(collection.TopologicallySortedComponents());
    }

    [Pure]
    public static void BeginExpose(object o) {}

    [Pure]
    public static void EndExpose() {}

    [Pure]
    public static void LoopInvariant(bool p) {
      Contract.Assert(p);
    }

    public static class Owner {
      [Pure]
      public static bool Same(object o, object p) {
        return true;
      }

      [Pure]
      public static void AssignSame(object o, object p) {}

      [Pure]
      public static object ElementProxy(object o) {
        return o;
      }

      [Pure]
      public static bool None(object o) {
        return true;
      }
    }

    [Serializable]
    public class UnreachableException : Exception {}
  }

  public class PeerAttribute : Attribute {}

  public class RepAttribute : Attribute {}

  public class CapturedAttribute : Attribute {}

  public class NotDelayedAttribute : Attribute {}

  public class NoDefaultContractAttribute : Attribute {}

  public class VerifyAttribute : Attribute {
    public VerifyAttribute(bool b) {}
  }

  public class StrictReadonlyAttribute : Attribute {}

  public class AdditiveAttribute : Attribute {}

  public class ReadsAttribute : Attribute {
    public enum Reads {
      Nothing
    }

    public ReadsAttribute(object o) {}
  }
}