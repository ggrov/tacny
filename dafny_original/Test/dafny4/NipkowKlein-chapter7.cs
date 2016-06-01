// Dafny program NipkowKlein-chapter7.dfy compiled into C#
// To recompile, use 'csc' with: /r:System.Numerics.dll
// and choosing /target:exe or /target:library
// You might also want to include compiler switches like:
//     /debug /nowarn:0164 /nowarn:0219

using System; // for Func
using System.Numerics;

namespace Dafny
{
  using System.Collections.Generic;

  public class Set<T>
  {
    Dictionary<T, bool> dict;
    Set(Dictionary<T, bool> d) {
      dict = d;
    }
    public static Set<T> Empty {
      get {
        return new Set<T>(new Dictionary<T, bool>(0));
      }
    }
    public static Set<T> FromElements(params T[] values) {
      Dictionary<T, bool> d = new Dictionary<T, bool>(values.Length);
      foreach (T t in values)
        d[t] = true;
      return new Set<T>(d);
    }
    public static Set<T> FromCollection(ICollection<T> values) {
      Dictionary<T, bool> d = new Dictionary<T, bool>();
      foreach (T t in values)
        d[t] = true;
      return new Set<T>(d);
    }
    public int Length {
      get { return dict.Count; }
    }
    public long LongLength {
      get { return dict.Count; }
    }
    public IEnumerable<T> Elements {
      get {
        return dict.Keys;
      }
    }
    /// <summary>
    /// This is an inefficient iterator for producing all subsets of "this".  Each set returned is the same
    /// Set<T> object (but this Set<T> object is fresh; in particular, it is not "this").
    /// </summary>
    public IEnumerable<Set<T>> AllSubsets {
      get {
        // Start by putting all set elements into a list
        var elmts = new List<T>();
        elmts.AddRange(dict.Keys);
        var n = elmts.Count;
        var which = new bool[n];
        var s = new Set<T>(new Dictionary<T, bool>(0));
        while (true) {
          yield return s;
          // "add 1" to "which", as if doing a carry chain.  For every digit changed, change the membership of the corresponding element in "s".
          int i = 0;
          for (; i < n && which[i]; i++) {
            which[i] = false;
            s.dict.Remove(elmts[i]);
          }
          if (i == n) {
            // we have cycled through all the subsets
            break;
          }
          which[i] = true;
          s.dict.Add(elmts[i], true);
        }
      }
    }
    public bool Equals(Set<T> other) {
      return dict.Count == other.dict.Count && IsSubsetOf(other);
    }
    public override bool Equals(object other) {
      return other is Set<T> && Equals((Set<T>)other);
    }
    public override int GetHashCode() {
      var hashCode = 1;
      foreach (var t in dict.Keys) {
        hashCode = hashCode * (t.GetHashCode()+3);
      }
      return hashCode;
    }
    public override string ToString() {
      var s = "{";
      var sep = "";
      foreach (var t in dict.Keys) {
        s += sep + t.ToString();
        sep = ", ";
      }
      return s + "}";
    }
    public bool IsProperSubsetOf(Set<T> other) {
      return dict.Count < other.dict.Count && IsSubsetOf(other);
    }
    public bool IsSubsetOf(Set<T> other) {
      if (other.dict.Count < dict.Count)
        return false;
      foreach (T t in dict.Keys) {
        if (!other.dict.ContainsKey(t))
          return false;
      }
      return true;
    }
    public bool IsSupersetOf(Set<T> other) {
      return other.IsSubsetOf(this);
    }
    public bool IsProperSupersetOf(Set<T> other) {
      return other.IsProperSubsetOf(this);
    }
    public bool IsDisjointFrom(Set<T> other) {
      Dictionary<T, bool> a, b;
      if (dict.Count < other.dict.Count) {
        a = dict; b = other.dict;
      } else {
        a = other.dict; b = dict;
      }
      foreach (T t in a.Keys) {
        if (b.ContainsKey(t))
          return false;
      }
      return true;
    }
    public bool Contains(T t) {
      return dict.ContainsKey(t);
    }
    public Set<T> Union(Set<T> other) {
      if (dict.Count == 0)
        return other;
      else if (other.dict.Count == 0)
        return this;
      Dictionary<T, bool> a, b;
      if (dict.Count < other.dict.Count) {
        a = dict; b = other.dict;
      } else {
        a = other.dict; b = dict;
      }
      Dictionary<T, bool> r = new Dictionary<T, bool>();
      foreach (T t in b.Keys)
        r[t] = true;
      foreach (T t in a.Keys)
        r[t] = true;
      return new Set<T>(r);
    }
    public Set<T> Intersect(Set<T> other) {
      if (dict.Count == 0)
        return this;
      else if (other.dict.Count == 0)
        return other;
      Dictionary<T, bool> a, b;
      if (dict.Count < other.dict.Count) {
        a = dict; b = other.dict;
      } else {
        a = other.dict; b = dict;
      }
      var r = new Dictionary<T, bool>();
      foreach (T t in a.Keys) {
        if (b.ContainsKey(t))
          r.Add(t, true);
      }
      return new Set<T>(r);
    }
    public Set<T> Difference(Set<T> other) {
      if (dict.Count == 0)
        return this;
      else if (other.dict.Count == 0)
        return this;
      var r = new Dictionary<T, bool>();
      foreach (T t in dict.Keys) {
        if (!other.dict.ContainsKey(t))
          r.Add(t, true);
      }
      return new Set<T>(r);
    }
  }
  public class MultiSet<T>
  {
    Dictionary<T, int> dict;
    MultiSet(Dictionary<T, int> d) {
      dict = d;
    }
    public static MultiSet<T> Empty {
      get {
        return new MultiSet<T>(new Dictionary<T, int>(0));
      }
    }
    public static MultiSet<T> FromElements(params T[] values) {
      Dictionary<T, int> d = new Dictionary<T, int>(values.Length);
      foreach (T t in values) {
        var i = 0;
        if (!d.TryGetValue(t, out i)) {
          i = 0;
        }
        d[t] = i + 1;
      }
      return new MultiSet<T>(d);
    }
    public static MultiSet<T> FromCollection(ICollection<T> values) {
      Dictionary<T, int> d = new Dictionary<T, int>();
      foreach (T t in values) {
        var i = 0;
        if (!d.TryGetValue(t, out i)) {
          i = 0;
        }
        d[t] = i + 1;
      }
      return new MultiSet<T>(d);
    }
    public static MultiSet<T> FromSeq(Sequence<T> values) {
      Dictionary<T, int> d = new Dictionary<T, int>();
      foreach (T t in values.Elements) {
        var i = 0;
        if (!d.TryGetValue(t, out i)) {
          i = 0;
        }
        d[t] = i + 1;
      }
      return new MultiSet<T>(d);
    }
    public static MultiSet<T> FromSet(Set<T> values) {
      Dictionary<T, int> d = new Dictionary<T, int>();
      foreach (T t in values.Elements) {
        d[t] = 1;
      }
      return new MultiSet<T>(d);
    }

    public bool Equals(MultiSet<T> other) {
      return other.IsSubsetOf(this) && this.IsSubsetOf(other);
    }
    public override bool Equals(object other) {
      return other is MultiSet<T> && Equals((MultiSet<T>)other);
    }
    public override int GetHashCode() {
      var hashCode = 1;
      foreach (var kv in dict) {
        var key = kv.Key.GetHashCode();
        key = (key << 3) | (key >> 29) ^ kv.Value.GetHashCode();
        hashCode = hashCode * (key + 3);
      }
      return hashCode;
    }
    public override string ToString() {
      var s = "multiset{";
      var sep = "";
      foreach (var kv in dict) {
        var t = kv.Key.ToString();
        for (int i = 0; i < kv.Value; i++) {
          s += sep + t.ToString();
          sep = ", ";
        }
      }
      return s + "}";
    }
    public bool IsProperSubsetOf(MultiSet<T> other) {
      return !Equals(other) && IsSubsetOf(other);
    }
    public bool IsSubsetOf(MultiSet<T> other) {
      foreach (T t in dict.Keys) {
        if (!other.dict.ContainsKey(t) || other.dict[t] < dict[t])
          return false;
      }
      return true;
    }
    public bool IsSupersetOf(MultiSet<T> other) {
      return other.IsSubsetOf(this);
    }
    public bool IsProperSupersetOf(MultiSet<T> other) {
      return other.IsProperSubsetOf(this);
    }
    public bool IsDisjointFrom(MultiSet<T> other) {
      foreach (T t in dict.Keys) {
        if (other.dict.ContainsKey(t))
          return false;
      }
      foreach (T t in other.dict.Keys) {
        if (dict.ContainsKey(t))
          return false;
      }
      return true;
    }
    public bool Contains(T t) {
      return dict.ContainsKey(t);
    }
    public MultiSet<T> Union(MultiSet<T> other) {
      if (dict.Count == 0)
        return other;
      else if (other.dict.Count == 0)
        return this;
      var r = new Dictionary<T, int>();
      foreach (T t in dict.Keys) {
        var i = 0;
        if (!r.TryGetValue(t, out i)) {
          i = 0;
        }
        r[t] = i + dict[t];
      }
      foreach (T t in other.dict.Keys) {
        var i = 0;
        if (!r.TryGetValue(t, out i)) {
          i = 0;
        }
        r[t] = i + other.dict[t];
      }
      return new MultiSet<T>(r);
    }
    public MultiSet<T> Intersect(MultiSet<T> other) {
      if (dict.Count == 0)
        return this;
      else if (other.dict.Count == 0)
        return other;
      var r = new Dictionary<T, int>();
      foreach (T t in dict.Keys) {
        if (other.dict.ContainsKey(t)) {
          r.Add(t, other.dict[t] < dict[t] ? other.dict[t] : dict[t]);
        }
      }
      return new MultiSet<T>(r);
    }
    public MultiSet<T> Difference(MultiSet<T> other) { // \result == this - other
      if (dict.Count == 0)
        return this;
      else if (other.dict.Count == 0)
        return this;
      var r = new Dictionary<T, int>();
      foreach (T t in dict.Keys) {
        if (!other.dict.ContainsKey(t)) {
          r.Add(t, dict[t]);
        } else if (other.dict[t] < dict[t]) {
          r.Add(t, dict[t] - other.dict[t]);
        }
      }
      return new MultiSet<T>(r);
    }
    public IEnumerable<T> Elements {
      get {
        List<T> l = new List<T>();
        foreach (T t in dict.Keys) {
          int n;
          dict.TryGetValue(t, out n);
          for (int i = 0; i < n; i ++) {
            l.Add(t);
          }
        }
        return l;
      }
    }
  }

  public class Map<U, V>
  {
    Dictionary<U, V> dict;
    Map(Dictionary<U, V> d) {
      dict = d;
    }
    public static Map<U, V> Empty {
      get {
        return new Map<U, V>(new Dictionary<U,V>());
      }
    }
    public static Map<U, V> FromElements(params Pair<U, V>[] values) {
      Dictionary<U, V> d = new Dictionary<U, V>(values.Length);
      foreach (Pair<U, V> p in values) {
        d[p.Car] = p.Cdr;
      }
      return new Map<U, V>(d);
    }
    public static Map<U, V> FromCollection(List<Pair<U, V>> values) {
      Dictionary<U, V> d = new Dictionary<U, V>(values.Count);
      foreach (Pair<U, V> p in values) {
        d[p.Car] = p.Cdr;
      }
      return new Map<U, V>(d);
    }
    public int Length {
      get { return dict.Count; }
    }
    public long LongLength {
      get { return dict.Count; }
    }
    public bool Equals(Map<U, V> other) {
      foreach (U u in dict.Keys) {
        V v1, v2;
        if (!dict.TryGetValue(u, out v1)) {
          return false; // this shouldn't happen
        }
        if (!other.dict.TryGetValue(u, out v2)) {
          return false; // other dictionary does not contain this element
        }
        if (!v1.Equals(v2)) {
          return false;
        }
      }
      foreach (U u in other.dict.Keys) {
        if (!dict.ContainsKey(u)) {
          return false; // this shouldn't happen
        }
      }
      return true;
    }
    public override bool Equals(object other) {
      return other is Map<U, V> && Equals((Map<U, V>)other);
    }
    public override int GetHashCode() {
      var hashCode = 1;
      foreach (var kv in dict) {
        var key = kv.Key.GetHashCode();
        key = (key << 3) | (key >> 29) ^ kv.Value.GetHashCode();
        hashCode = hashCode * (key + 3);
      }
      return hashCode;
    }
    public override string ToString() {
      var s = "map[";
      var sep = "";
      foreach (var kv in dict) {
        s += sep + kv.Key.ToString() + " := " + kv.Value.ToString();
        sep = ", ";
      }
      return s + "]";
    }
    public bool IsDisjointFrom(Map<U, V> other) {
      foreach (U u in dict.Keys) {
        if (other.dict.ContainsKey(u))
          return false;
      }
      foreach (U u in other.dict.Keys) {
        if (dict.ContainsKey(u))
          return false;
      }
      return true;
    }
    public bool Contains(U u) {
      return dict.ContainsKey(u);
    }
    public V Select(U index) {
      return dict[index];
    }
    public Map<U, V> Update(U index, V val) {
      Dictionary<U, V> d = new Dictionary<U, V>(dict);
      d[index] = val;
      return new Map<U, V>(d);
    }
    public IEnumerable<U> Domain {
      get {
        return dict.Keys;
      }
    }
  }
  public class Sequence<T>
  {
    T[] elmts;
    public Sequence(T[] ee) {
      elmts = ee;
    }
    public static Sequence<T> Empty {
      get {
        return new Sequence<T>(new T[0]);
      }
    }
    public static Sequence<T> FromElements(params T[] values) {
      return new Sequence<T>(values);
    }
    public static Sequence<char> FromString(string s) {
      return new Sequence<char>(s.ToCharArray());
    }
    public int Length {
      get { return elmts.Length; }
    }
    public long LongLength {
      get { return elmts.LongLength; }
    }
    public T[] Elements {
      get {
        return elmts;
      }
    }
    public IEnumerable<T> UniqueElements {
      get {
        var st = Set<T>.FromElements(elmts);
        return st.Elements;
      }
    }
    public T Select(ulong index) {
      return elmts[index];
    }
    public T Select(long index) {
      return elmts[index];
    }
    public T Select(uint index) {
      return elmts[index];
    }
    public T Select(int index) {
      return elmts[index];
    }
    public T Select(BigInteger index) {
      return elmts[(int)index];
    }
    public Sequence<T> Update(long index, T t) {
      T[] a = (T[])elmts.Clone();
      a[index] = t;
      return new Sequence<T>(a);
    }
    public Sequence<T> Update(ulong index, T t) {
      return Update((long)index, t);
    }
    public Sequence<T> Update(BigInteger index, T t) {
      return Update((long)index, t);
    }
    public bool Equals(Sequence<T> other) {
      int n = elmts.Length;
      return n == other.elmts.Length && EqualUntil(other, n);
    }
    public override bool Equals(object other) {
      return other is Sequence<T> && Equals((Sequence<T>)other);
    }
    public override int GetHashCode() {
      if (elmts == null || elmts.Length == 0)
        return 0;
      var hashCode = 0;
      for (var i = 0; i < elmts.Length; i++) {
        hashCode = (hashCode << 3) | (hashCode >> 29) ^ elmts[i].GetHashCode();
      }
      return hashCode;
    }
    public override string ToString() {
      if (elmts is char[]) {
        var s = "";
        foreach (var t in elmts) {
          s += t.ToString();
        }
        return s;
      } else {
        var s = "[";
        var sep = "";
        foreach (var t in elmts) {
          s += sep + t.ToString();
          sep = ", ";
        }
        return s + "]";
      }
    }
    bool EqualUntil(Sequence<T> other, int n) {
      for (int i = 0; i < n; i++) {
        if (!elmts[i].Equals(other.elmts[i]))
          return false;
      }
      return true;
    }
    public bool IsProperPrefixOf(Sequence<T> other) {
      int n = elmts.Length;
      return n < other.elmts.Length && EqualUntil(other, n);
    }
    public bool IsPrefixOf(Sequence<T> other) {
      int n = elmts.Length;
      return n <= other.elmts.Length && EqualUntil(other, n);
    }
    public Sequence<T> Concat(Sequence<T> other) {
      if (elmts.Length == 0)
        return other;
      else if (other.elmts.Length == 0)
        return this;
      T[] a = new T[elmts.Length + other.elmts.Length];
      System.Array.Copy(elmts, 0, a, 0, elmts.Length);
      System.Array.Copy(other.elmts, 0, a, elmts.Length, other.elmts.Length);
      return new Sequence<T>(a);
    }
    public bool Contains(T t) {
      int n = elmts.Length;
      for (int i = 0; i < n; i++) {
        if (t.Equals(elmts[i]))
          return true;
      }
      return false;
    }
    public Sequence<T> Take(long m) {
      if (elmts.LongLength == m)
        return this;
      T[] a = new T[m];
      System.Array.Copy(elmts, a, m);
      return new Sequence<T>(a);
    }
    public Sequence<T> Take(ulong n) {
      return Take((long)n);
    }
    public Sequence<T> Take(BigInteger n) {
      return Take((long)n);
    }
    public Sequence<T> Drop(long m) {
      if (m == 0)
        return this;
      T[] a = new T[elmts.Length - m];
      System.Array.Copy(elmts, m, a, 0, elmts.Length - m);
      return new Sequence<T>(a);
    }
    public Sequence<T> Drop(ulong n) {
      return Drop((long)n);
    }
    public Sequence<T> Drop(BigInteger n) {
      if (n.IsZero)
        return this;
      return Drop((long)n);
    }
  }
  public struct Pair<A, B>
  {
    public readonly A Car;
    public readonly B Cdr;
    public Pair(A a, B b) {
      this.Car = a;
      this.Cdr = b;
    }
  }
  public partial class Helpers {
    // Computing forall/exists quantifiers
    public static bool QuantBool(bool frall, System.Predicate<bool> pred) {
      if (frall) {
        return pred(false) && pred(true);
      } else {
        return pred(false) || pred(true);
      }
    }
    public static bool QuantInt(BigInteger lo, BigInteger hi, bool frall, System.Predicate<BigInteger> pred) {
      for (BigInteger i = lo; i < hi; i++) {
        if (pred(i) != frall) { return !frall; }
      }
      return frall;
    }
    public static bool QuantSet<U>(Dafny.Set<U> set, bool frall, System.Predicate<U> pred) {
      foreach (var u in set.Elements) {
        if (pred(u) != frall) { return !frall; }
      }
      return frall;
    }
    public static bool QuantMap<U,V>(Dafny.Map<U,V> map, bool frall, System.Predicate<U> pred) {
      foreach (var u in map.Domain) {
        if (pred(u) != frall) { return !frall; }
      }
      return frall;
    }
    public static bool QuantSeq<U>(Dafny.Sequence<U> seq, bool frall, System.Predicate<U> pred) {
      foreach (var u in seq.Elements) {
        if (pred(u) != frall) { return !frall; }
      }
      return frall;
    }
    public static bool QuantDatatype<U>(IEnumerable<U> set, bool frall, System.Predicate<U> pred) {
      foreach (var u in set) {
        if (pred(u) != frall) { return !frall; }
      }
      return frall;
    }
    // Enumerating other collections
    public delegate Dafny.Set<T> ComprehensionDelegate<T>();
    public delegate Dafny.Map<U, V> MapComprehensionDelegate<U, V>();
    public static IEnumerable<bool> AllBooleans {
      get {
        yield return false;
        yield return true;
      }
    }
    public static IEnumerable<BigInteger> AllIntegers {
      get {
        yield return new BigInteger(0);
        for (var j = new BigInteger(1);; j++) {
          yield return j;
          yield return -j;
        }
      }
    }
    // pre: b != 0
    // post: result == a/b, as defined by Euclidean Division (http://en.wikipedia.org/wiki/Modulo_operation)
    public static sbyte EuclideanDivision_sbyte(sbyte a, sbyte b) {
      return (sbyte)EuclideanDivision_int(a, b);
    }
    public static short EuclideanDivision_short(short a, short b) {
      return (short)EuclideanDivision_int(a, b);
    }
    public static int EuclideanDivision_int(int a, int b) {
      if (0 <= a) {
        if (0 <= b) {
          // +a +b: a/b
          return (int)(((uint)(a)) / ((uint)(b)));
        } else {
          // +a -b: -(a/(-b))
          return -((int)(((uint)(a)) / ((uint)(unchecked(-b)))));
        }
      } else {
        if (0 <= b) {
          // -a +b: -((-a-1)/b) - 1
          return -((int)(((uint)(-(a + 1))) / ((uint)(b)))) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return ((int)(((uint)(-(a + 1))) / ((uint)(unchecked(-b))))) + 1;
        }
      }
    }
    public static long EuclideanDivision_long(long a, long b) {
      if (0 <= a) {
        if (0 <= b) {
          // +a +b: a/b
          return (long)(((ulong)(a)) / ((ulong)(b)));
        } else {
          // +a -b: -(a/(-b))
          return -((long)(((ulong)(a)) / ((ulong)(unchecked(-b)))));
        }
      } else {
        if (0 <= b) {
          // -a +b: -((-a-1)/b) - 1
          return -((long)(((ulong)(-(a + 1))) / ((ulong)(b)))) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return ((long)(((ulong)(-(a + 1))) / ((ulong)(unchecked(-b))))) + 1;
        }
      }
    }
    public static BigInteger EuclideanDivision(BigInteger a, BigInteger b) {
      if (0 <= a.Sign) {
        if (0 <= b.Sign) {
          // +a +b: a/b
          return BigInteger.Divide(a, b);
        } else {
          // +a -b: -(a/(-b))
          return BigInteger.Negate(BigInteger.Divide(a, BigInteger.Negate(b)));
        }
      } else {
        if (0 <= b.Sign) {
          // -a +b: -((-a-1)/b) - 1
          return BigInteger.Negate(BigInteger.Divide(BigInteger.Negate(a) - 1, b)) - 1;
        } else {
          // -a -b: ((-a-1)/(-b)) + 1
          return BigInteger.Divide(BigInteger.Negate(a) - 1, BigInteger.Negate(b)) + 1;
        }
      }
    }
    // pre: b != 0
    // post: result == a%b, as defined by Euclidean Division (http://en.wikipedia.org/wiki/Modulo_operation)
    public static sbyte EuclideanModulus_sbyte(sbyte a, sbyte b) {
      return (sbyte)EuclideanModulus_int(a, b);
    }
    public static short EuclideanModulus_short(short a, short b) {
      return (short)EuclideanModulus_int(a, b);
    }
    public static int EuclideanModulus_int(int a, int b) {
      uint bp = (0 <= b) ? (uint)b : (uint)(unchecked(-b));
      if (0 <= a) {
        // +a: a % b'
        return (int)(((uint)a) % bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        uint c = ((uint)(unchecked(-a))) % bp;
        return (int)(c == 0 ? c : bp - c);
      }
    }
    public static long EuclideanModulus_long(long a, long b) {
      ulong bp = (0 <= b) ? (ulong)b : (ulong)(unchecked(-b));
      if (0 <= a) {
        // +a: a % b'
        return (long)(((ulong)a) % bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        ulong c = ((ulong)(unchecked(-a))) % bp;
        return (long)(c == 0 ? c : bp - c);
      }
    }
    public static BigInteger EuclideanModulus(BigInteger a, BigInteger b) {
      var bp = BigInteger.Abs(b);
      if (0 <= a.Sign) {
        // +a: a % b'
        return BigInteger.Remainder(a, bp);
      } else {
        // c = ((-a) % b')
        // -a: b' - c if c > 0
        // -a: 0 if c == 0
        var c = BigInteger.Remainder(BigInteger.Negate(a), bp);
        return c.IsZero ? c : BigInteger.Subtract(bp, c);
      }
    }
    public static Sequence<T> SeqFromArray<T>(T[] array) {
      return new Sequence<T>(array);
    }
    // In .NET version 4.5, it it possible to mark a method with "AggressiveInlining", which says to inline the
    // method if possible.  Method "ExpressionSequence" would be a good candidate for it:
    // [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static U ExpressionSequence<T, U>(T t, U u)
    {
      return u;
    }

    public static U Let<T, U>(T t, Func<T,U> f) {
      return f(t);
    }

    public delegate Result Function<Input,Result>(Input input);

    public static A Id<A>(A a) {
      return a;
    }
  }

  public struct BigRational
  {
    public static readonly BigRational ZERO = new BigRational(0);

    BigInteger num, den;  // invariant 1 <= den
    public override string ToString() {
      return string.Format("({0}.0 / {1}.0)", num, den);
    }
    public BigRational(int n) {
      num = new BigInteger(n);
      den = BigInteger.One;
    }
    public BigRational(BigInteger n, BigInteger d) {
      // requires 1 <= d
      num = n;
      den = d;
    }
    public BigInteger ToBigInteger() {
      if (0 <= num) {
        return num / den;
      } else {
        return (num - den + 1) / den;
      }
    }
    /// <summary>
    /// Returns values such that aa/dd == a and bb/dd == b.
    /// </summary>
    private static void Normalize(BigRational a, BigRational b, out BigInteger aa, out BigInteger bb, out BigInteger dd) {
      var gcd = BigInteger.GreatestCommonDivisor(a.den, b.den);
      var xx = a.den / gcd;
      var yy = b.den / gcd;
      // We now have a == a.num / (xx * gcd) and b == b.num / (yy * gcd).
      aa = a.num * yy;
      bb = b.num * xx;
      dd = a.den * yy;
    }
    public int CompareTo(BigRational that) {
      // simple things first
      int asign = this.num.Sign;
      int bsign = that.num.Sign;
      if (asign < 0 && 0 <= bsign) {
        return 1;
      } else if (asign <= 0 && 0 < bsign) {
        return 1;
      } else if (bsign < 0 && 0 <= asign) {
        return -1;
      } else if (bsign <= 0 && 0 < asign) {
        return -1;
      }
      BigInteger aa, bb, dd;
      Normalize(this, that, out aa, out bb, out dd);
      return aa.CompareTo(bb);
    }
    public override int GetHashCode() {
      return num.GetHashCode() + 29 * den.GetHashCode();
    }
    public override bool Equals(object obj) {
      if (obj is BigRational) {
        return this == (BigRational)obj;
      } else {
        return false;
      }
    }
    public static bool operator ==(BigRational a, BigRational b) {
      return a.CompareTo(b) == 0;
    }
    public static bool operator !=(BigRational a, BigRational b) {
      return a.CompareTo(b) != 0;
    }
    public static bool operator >(BigRational a, BigRational b) {
      return 0 < a.CompareTo(b);
    }
    public static bool operator >=(BigRational a, BigRational b) {
      return 0 <= a.CompareTo(b);
    }
    public static bool operator <(BigRational a, BigRational b) {
      return a.CompareTo(b) < 0;
    }
    public static bool operator <=(BigRational a, BigRational b) {
      return a.CompareTo(b) <= 0;
    }
    public static BigRational operator +(BigRational a, BigRational b) {
      BigInteger aa, bb, dd;
      Normalize(a, b, out aa, out bb, out dd);
      return new BigRational(aa + bb, dd);
    }
    public static BigRational operator -(BigRational a, BigRational b) {
      BigInteger aa, bb, dd;
      Normalize(a, b, out aa, out bb, out dd);
      return new BigRational(aa - bb, dd);
    }
    public static BigRational operator -(BigRational a) {
      return new BigRational(-a.num, a.den);
    }
    public static BigRational operator *(BigRational a, BigRational b) {
      return new BigRational(a.num * b.num, a.den * b.den);
    }
    public static BigRational operator /(BigRational a, BigRational b) {
      // Compute the reciprocal of b
      BigRational bReciprocal;
      if (0 < b.num) {
        bReciprocal = new BigRational(b.den, b.num);
      } else {
        // this is the case b.num < 0
        bReciprocal = new BigRational(-b.den, -b.num);
      }
      return a * bReciprocal;
    }
  }
}
namespace Dafny {
  public partial class Helpers {
      public static T[] InitNewArray1<T>(BigInteger size0) {
        int s0 = (int)size0;
        T[] a = new T[s0];
        BigInteger[] b = a as BigInteger[];
        if (b != null) {
          BigInteger z = new BigInteger(0);
          for (int i0 = 0; i0 < s0; i0++)
            b[i0] = z;
        }
        return a;
      }
  }
}
namespace @_System {


  public abstract class Base___tuple_h2<@T0,@T1> { }
  public class __tuple_h2____hMake<@T0,@T1> : Base___tuple_h2<@T0,@T1> {
    public readonly @T0 @_0;
    public readonly @T1 @_1;
    public __tuple_h2____hMake(@T0 @_0, @T1 @_1) {
      this.@_0 = @_0;
      this.@_1 = @_1;
    }
    public override bool Equals(object other) {
      var oth = other as _System.@__tuple_h2____hMake<@T0,@T1>;
      return oth != null && this.@_0.Equals(oth.@_0) && this.@_1.Equals(oth.@_1);
    }
    public override int GetHashCode() {
      return 0 ^ this.@_0.GetHashCode() ^ this.@_1.GetHashCode();
    }
    public override string ToString() {
      string s = "";
      s += "(";
      s += @_0.ToString();
      s += ", ";
      s += @_1.ToString();
      s += ")";
      return s;
    }
  }
  public struct @__tuple_h2<@T0,@T1> {
    Base___tuple_h2<@T0,@T1> _d;
    public Base___tuple_h2<@T0,@T1> _D {
      get {
        if (_d == null) {
          _d = Default;
        }
        return _d;
      }
    }
    public @__tuple_h2(Base___tuple_h2<@T0,@T1> d) { this._d = d; }
    static Base___tuple_h2<@T0,@T1> theDefault;
    public static Base___tuple_h2<@T0,@T1> Default {
      get {
        if (theDefault == null) {
          theDefault = new _System.@__tuple_h2____hMake<@T0,@T1>(default(@T0), default(@T1));
        }
        return theDefault;
      }
    }
    public override bool Equals(object other) {
      return other is @__tuple_h2<@T0,@T1> && _D.Equals(((@__tuple_h2<@T0,@T1>)other)._D);
    }
    public override int GetHashCode() { return _D.GetHashCode(); }
    public override string ToString() { return _D.ToString(); }
    public bool is____hMake { get { return _D is __tuple_h2____hMake<@T0,@T1>; } }
    public @T0 dtor__0 { get { return ((__tuple_h2____hMake<@T0,@T1>)_D).@_0; } }
    public @T1 dtor__1 { get { return ((__tuple_h2____hMake<@T0,@T1>)_D).@_1; } }
  }
} // end of namespace _System

public abstract class Base_List<@T> { }
public class List_Nil<@T> : Base_List<@T> {
  public List_Nil() {
  }
  public override bool Equals(object other) {
    var oth = other as List_Nil<@T>;
    return oth != null;
  }
  public override int GetHashCode() {
    return 0;
  }
  public override string ToString() {
    string s = "List.Nil";
    return s;
  }
}
public class List_Cons<@T> : Base_List<@T> {
  public readonly @T @head;
  public readonly @List<@T> @tail;
  public List_Cons(@T @head, @List<@T> @tail) {
    this.@head = @head;
    this.@tail = @tail;
  }
  public override bool Equals(object other) {
    var oth = other as List_Cons<@T>;
    return oth != null && this.@head.Equals(oth.@head) && this.@tail.Equals(oth.@tail);
  }
  public override int GetHashCode() {
    return 0 ^ this.@head.GetHashCode() ^ this.@tail.GetHashCode();
  }
  public override string ToString() {
    string s = "List.Cons";
    s += "(";
    s += @head.ToString();
    s += ", ";
    s += @tail.ToString();
    s += ")";
    return s;
  }
}
public struct @List<@T> {
  Base_List<@T> _d;
  public Base_List<@T> _D {
    get {
      if (_d == null) {
        _d = Default;
      }
      return _d;
    }
  }
  public @List(Base_List<@T> d) { this._d = d; }
  static Base_List<@T> theDefault;
  public static Base_List<@T> Default {
    get {
      if (theDefault == null) {
        theDefault = new List_Nil<@T>();
      }
      return theDefault;
    }
  }
  public override bool Equals(object other) {
    return other is @List<@T> && _D.Equals(((@List<@T>)other)._D);
  }
  public override int GetHashCode() { return _D.GetHashCode(); }
  public override string ToString() { return _D.ToString(); }
  public bool is_Nil { get { return _D is List_Nil<@T>; } }
  public bool is_Cons { get { return _D is List_Cons<@T>; } }
  public @T dtor_head { get { return ((List_Cons<@T>)_D).@head; } }
  public @List<@T> dtor_tail { get { return ((List_Cons<@T>)_D).@tail; } }
}




public abstract class Base_aexp { }
public class aexp_N : Base_aexp {
  public readonly BigInteger @n;
  public aexp_N(BigInteger @n) {
    this.@n = @n;
  }
  public override bool Equals(object other) {
    var oth = other as aexp_N;
    return oth != null && this.@n.Equals(oth.@n);
  }
  public override int GetHashCode() {
    return 0 ^ this.@n.GetHashCode();
  }
  public override string ToString() {
    string s = "aexp.N";
    s += "(";
    s += @n.ToString();
    s += ")";
    return s;
  }
}
public class aexp_V : Base_aexp {
  public readonly Dafny.Sequence<char> @x;
  public aexp_V(Dafny.Sequence<char> @x) {
    this.@x = @x;
  }
  public override bool Equals(object other) {
    var oth = other as aexp_V;
    return oth != null && this.@x.Equals(oth.@x);
  }
  public override int GetHashCode() {
    return 0 ^ this.@x.GetHashCode();
  }
  public override string ToString() {
    string s = "aexp.V";
    s += "(";
    s += @x.ToString();
    s += ")";
    return s;
  }
}
public class aexp_Plus : Base_aexp {
  public readonly @aexp @_0;
  public readonly @aexp @_1;
  public aexp_Plus(@aexp @_0, @aexp @_1) {
    this.@_0 = @_0;
    this.@_1 = @_1;
  }
  public override bool Equals(object other) {
    var oth = other as aexp_Plus;
    return oth != null && this.@_0.Equals(oth.@_0) && this.@_1.Equals(oth.@_1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_0.GetHashCode() ^ this.@_1.GetHashCode();
  }
  public override string ToString() {
    string s = "aexp.Plus";
    s += "(";
    s += @_0.ToString();
    s += ", ";
    s += @_1.ToString();
    s += ")";
    return s;
  }
}
public struct @aexp {
  Base_aexp _d;
  public Base_aexp _D {
    get {
      if (_d == null) {
        _d = Default;
      }
      return _d;
    }
  }
  public @aexp(Base_aexp d) { this._d = d; }
  static Base_aexp theDefault;
  public static Base_aexp Default {
    get {
      if (theDefault == null) {
        theDefault = new aexp_N(BigInteger.Zero);
      }
      return theDefault;
    }
  }
  public override bool Equals(object other) {
    return other is @aexp && _D.Equals(((@aexp)other)._D);
  }
  public override int GetHashCode() { return _D.GetHashCode(); }
  public override string ToString() { return _D.ToString(); }
  public bool is_N { get { return _D is aexp_N; } }
  public bool is_V { get { return _D is aexp_V; } }
  public bool is_Plus { get { return _D is aexp_Plus; } }
  public BigInteger dtor_n { get { return ((aexp_N)_D).@n; } }
  public Dafny.Sequence<char> dtor_x { get { return ((aexp_V)_D).@x; } }
  public @aexp dtor__0 { get { return ((aexp_Plus)_D).@_0; } }
  public @aexp dtor__1 { get { return ((aexp_Plus)_D).@_1; } }
}

public abstract class Base_bexp { }
public class bexp_Bc : Base_bexp {
  public readonly bool @v;
  public bexp_Bc(bool @v) {
    this.@v = @v;
  }
  public override bool Equals(object other) {
    var oth = other as bexp_Bc;
    return oth != null && this.@v.Equals(oth.@v);
  }
  public override int GetHashCode() {
    return 0 ^ this.@v.GetHashCode();
  }
  public override string ToString() {
    string s = "bexp.Bc";
    s += "(";
    s += @v.ToString();
    s += ")";
    return s;
  }
}
public class bexp_Not : Base_bexp {
  public readonly @bexp @op;
  public bexp_Not(@bexp @op) {
    this.@op = @op;
  }
  public override bool Equals(object other) {
    var oth = other as bexp_Not;
    return oth != null && this.@op.Equals(oth.@op);
  }
  public override int GetHashCode() {
    return 0 ^ this.@op.GetHashCode();
  }
  public override string ToString() {
    string s = "bexp.Not";
    s += "(";
    s += @op.ToString();
    s += ")";
    return s;
  }
}
public class bexp_And : Base_bexp {
  public readonly @bexp @_0;
  public readonly @bexp @_1;
  public bexp_And(@bexp @_0, @bexp @_1) {
    this.@_0 = @_0;
    this.@_1 = @_1;
  }
  public override bool Equals(object other) {
    var oth = other as bexp_And;
    return oth != null && this.@_0.Equals(oth.@_0) && this.@_1.Equals(oth.@_1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_0.GetHashCode() ^ this.@_1.GetHashCode();
  }
  public override string ToString() {
    string s = "bexp.And";
    s += "(";
    s += @_0.ToString();
    s += ", ";
    s += @_1.ToString();
    s += ")";
    return s;
  }
}
public class bexp_Less : Base_bexp {
  public readonly @aexp @a0;
  public readonly @aexp @a1;
  public bexp_Less(@aexp @a0, @aexp @a1) {
    this.@a0 = @a0;
    this.@a1 = @a1;
  }
  public override bool Equals(object other) {
    var oth = other as bexp_Less;
    return oth != null && this.@a0.Equals(oth.@a0) && this.@a1.Equals(oth.@a1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@a0.GetHashCode() ^ this.@a1.GetHashCode();
  }
  public override string ToString() {
    string s = "bexp.Less";
    s += "(";
    s += @a0.ToString();
    s += ", ";
    s += @a1.ToString();
    s += ")";
    return s;
  }
}
public struct @bexp {
  Base_bexp _d;
  public Base_bexp _D {
    get {
      if (_d == null) {
        _d = Default;
      }
      return _d;
    }
  }
  public @bexp(Base_bexp d) { this._d = d; }
  static Base_bexp theDefault;
  public static Base_bexp Default {
    get {
      if (theDefault == null) {
        theDefault = new bexp_Bc(false);
      }
      return theDefault;
    }
  }
  public override bool Equals(object other) {
    return other is @bexp && _D.Equals(((@bexp)other)._D);
  }
  public override int GetHashCode() { return _D.GetHashCode(); }
  public override string ToString() { return _D.ToString(); }
  public bool is_Bc { get { return _D is bexp_Bc; } }
  public bool is_Not { get { return _D is bexp_Not; } }
  public bool is_And { get { return _D is bexp_And; } }
  public bool is_Less { get { return _D is bexp_Less; } }
  public bool dtor_v { get { return ((bexp_Bc)_D).@v; } }
  public @bexp dtor_op { get { return ((bexp_Not)_D).@op; } }
  public @bexp dtor__0 { get { return ((bexp_And)_D).@_0; } }
  public @bexp dtor__1 { get { return ((bexp_And)_D).@_1; } }
  public @aexp dtor_a0 { get { return ((bexp_Less)_D).@a0; } }
  public @aexp dtor_a1 { get { return ((bexp_Less)_D).@a1; } }
}

public abstract class Base_com { }
public class com_SKIP : Base_com {
  public com_SKIP() {
  }
  public override bool Equals(object other) {
    var oth = other as com_SKIP;
    return oth != null;
  }
  public override int GetHashCode() {
    return 0;
  }
  public override string ToString() {
    string s = "com.SKIP";
    return s;
  }
}
public class com_Assign : Base_com {
  public readonly Dafny.Sequence<char> @_a0;
  public readonly @aexp @_a1;
  public com_Assign(Dafny.Sequence<char> @_a0, @aexp @_a1) {
    this.@_a0 = @_a0;
    this.@_a1 = @_a1;
  }
  public override bool Equals(object other) {
    var oth = other as com_Assign;
    return oth != null && this.@_a0.Equals(oth.@_a0) && this.@_a1.Equals(oth.@_a1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_a0.GetHashCode() ^ this.@_a1.GetHashCode();
  }
  public override string ToString() {
    string s = "com.Assign";
    s += "(";
    s += @_a0.ToString();
    s += ", ";
    s += @_a1.ToString();
    s += ")";
    return s;
  }
}
public class com_Seq : Base_com {
  public readonly @com @_a0;
  public readonly @com @_a1;
  public com_Seq(@com @_a0, @com @_a1) {
    this.@_a0 = @_a0;
    this.@_a1 = @_a1;
  }
  public override bool Equals(object other) {
    var oth = other as com_Seq;
    return oth != null && this.@_a0.Equals(oth.@_a0) && this.@_a1.Equals(oth.@_a1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_a0.GetHashCode() ^ this.@_a1.GetHashCode();
  }
  public override string ToString() {
    string s = "com.Seq";
    s += "(";
    s += @_a0.ToString();
    s += ", ";
    s += @_a1.ToString();
    s += ")";
    return s;
  }
}
public class com_If : Base_com {
  public readonly @bexp @_a0;
  public readonly @com @_a1;
  public readonly @com @_a2;
  public com_If(@bexp @_a0, @com @_a1, @com @_a2) {
    this.@_a0 = @_a0;
    this.@_a1 = @_a1;
    this.@_a2 = @_a2;
  }
  public override bool Equals(object other) {
    var oth = other as com_If;
    return oth != null && this.@_a0.Equals(oth.@_a0) && this.@_a1.Equals(oth.@_a1) && this.@_a2.Equals(oth.@_a2);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_a0.GetHashCode() ^ this.@_a1.GetHashCode() ^ this.@_a2.GetHashCode();
  }
  public override string ToString() {
    string s = "com.If";
    s += "(";
    s += @_a0.ToString();
    s += ", ";
    s += @_a1.ToString();
    s += ", ";
    s += @_a2.ToString();
    s += ")";
    return s;
  }
}
public class com_While : Base_com {
  public readonly @bexp @_a0;
  public readonly @com @_a1;
  public com_While(@bexp @_a0, @com @_a1) {
    this.@_a0 = @_a0;
    this.@_a1 = @_a1;
  }
  public override bool Equals(object other) {
    var oth = other as com_While;
    return oth != null && this.@_a0.Equals(oth.@_a0) && this.@_a1.Equals(oth.@_a1);
  }
  public override int GetHashCode() {
    return 0 ^ this.@_a0.GetHashCode() ^ this.@_a1.GetHashCode();
  }
  public override string ToString() {
    string s = "com.While";
    s += "(";
    s += @_a0.ToString();
    s += ", ";
    s += @_a1.ToString();
    s += ")";
    return s;
  }
}
public struct @com {
  Base_com _d;
  public Base_com _D {
    get {
      if (_d == null) {
        _d = Default;
      }
      return _d;
    }
  }
  public @com(Base_com d) { this._d = d; }
  static Base_com theDefault;
  public static Base_com Default {
    get {
      if (theDefault == null) {
        theDefault = new com_SKIP();
      }
      return theDefault;
    }
  }
  public override bool Equals(object other) {
    return other is @com && _D.Equals(((@com)other)._D);
  }
  public override int GetHashCode() { return _D.GetHashCode(); }
  public override string ToString() { return _D.ToString(); }
  public bool is_SKIP { get { return _D is com_SKIP; } }
  public bool is_Assign { get { return _D is com_Assign; } }
  public bool is_Seq { get { return _D is com_Seq; } }
  public bool is_If { get { return _D is com_If; } }
  public bool is_While { get { return _D is com_While; } }
}

public class @__default {
}
