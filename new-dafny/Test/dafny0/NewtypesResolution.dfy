// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Cycle {
  type MySynonym = MyNewType  // error: a cycle
  newtype MyNewType = MyIntegerType_WellSupposedly
  type MyIntegerType_WellSupposedly = MySynonym
}

module MustBeNumeric {
  datatype List<T> = Nil | Cons(T, List)
  newtype NewDatatype = List<int>  // error: base type must be numeric based
}

module Goodies {
  newtype int32 = int
  newtype posReal = real
  newtype int8 = int32

  method M()
  {
    var k8 := new int8[100];
    var s: set<int32>;
    var x: posReal;
    var y: posReal;
    var z: int32;
    x := 5.3;
    z := 12;
    s := {};
    s := {40,20};
    x := x + y;
    var r0 := real(x);
    var r1: real := 2.0 * r0;
    var i0 := int(z);
    var i1: nat := 2 * i0;
    assert i1 == 24;
    assert r1 == 10.6;
    if x == r0 {  // error: cannot compare posReal and real
    } else if real(x) == r0 {
    } else if i0 == int(x) {
    } else if i0 == int(real(x)) {
    } else if real(i0) == real(x) {
    }
    if z == i0 {  // error: cannot compare int32 and int
    } else if int(z) == i0 {
    } else if r0 == real(z) {
    } else if r0 == real(int(z)) {
    } else if int(r0) == int(z) {
    }
    assert x == z;  // error: cannot compare posReal and int32
    assert x <= z;  // error: cannot compare posReal and int32
    assert z < i0;  // error: cannot compare int32 and int
    assert x > r1;  // error: cannot compare posReal and real

    var di := z % 2 - z / 2 + if z == 0 then 3 else 100 / z + 100 % z;
    var dr := x / 2.0 + if x == 0.0 then 3.0 else 100.0 / x;
    dr := dr % 3.0 + 3.0 % dr;  // error (x2): mod not supported for real-based types
    z, x := di, dr;

    var sq := [23, 50];
    assert forall ii :: 0 <= ii < |sq| ==> sq[ii] == sq[ii];
    var ms := multiset{23.0, 50.0};
    assert forall rr :: 0.0 <= rr < 100.0  ==> ms[rr] == ms[rr];

    var truncated := r0.Trunc + x.Trunc;
    var rounded := (r0 + 0.5).Trunc;
  }
}

module Constraints {
  newtype SmallInt = x: int | 0 <= x < 100
  newtype LargeInt = y: int | 0 <= y < 100

  newtype BadConstraint = a: SmallInt
    | a + a  // error: not a boolean
  newtype WotsDisVariable = u  :BadConstraint
    | u + a < 50  // error: undeclared identifier 'a'

  newtype A = x: int | 0 <= x
  newtype B = x: A | x < 100
  newtype C = B  // the constraints 0 <= x < 100 still apply

  predicate IsEven(x: int)  // note that this is a ghost predicate
  {
    x % 2 == 0
  }
  newtype G = x: int | IsEven(x)  // it's okay to use ghost constructs in type constraints

  newtype N = nat

  newtype OldState = y: real | old(y) == y  // error: old is not allowed in constraint

  newtype AssertType = s: int |
    var k := s;
    assert k == s;
    k < 10 || 10 <= s
}

module WrongNumberOfArguments {
  newtype N = nat
  method BadTypeArgs(n: N<int>)  // error: N takes no type arguments
}

module CyclicDependencies {
  newtype Cycle = x: int | (BadLemma(); false)  // error: cycle
  lemma BadLemma()
    ensures false;
  {
    var c: Cycle;
  }
}

module SelfCycleTest {
  newtype SelfCycle = x: int | var s: SelfCycle := 4; s < 10  // error: cyclic dependency on itself
}

module Module0 {
  import Module1
  method M(x: int) returns (n: Module1.N9) {
    var x := Module1.N9;  // error
  }
}

module Module1 {
  newtype N9 = int
}

module InferredType {
  newtype Int = x | 0 <= x < 100
  newtype Real = r | 0.0 <= r <= 100.0
  method M() returns (i: Int, r: Real)
  {
    i := 4;
    r := 4.0;
  }
  newtype AnotherInt = int
  method P(i: int, a: AnotherInt) returns (j: Int)
  {
    j := i;  // error: int not assignable to Int
    j := a;  // error: AnotherInt not assignable to Int
  }
}

module SynonymsAndModules {
  module M {
    type Syn = int
    type Y = Klass
    class Klass {
      static method H()
    }
  }

  method P()
  {
    var x: M.Syn;
    x := B;  // error: bad use of B
    x := M.Syn;  // error: bad use of M.Syn
    C.D();
    X.D();
    M.Klass.H();
    M.Y.H();
    M.Y.U();  // error: non-existent member U
  }

  type B = int
  type X = C
  class C {
    static method D()
  }
}

module MoreSynonyms {
  import SynonymLibrary

  type Syn<T> = Syn'<T,T>
  type Syn'<A,B> = C<B>
  class C<alpha> {
    static method MyMethod<beta>(b: beta, a: alpha)
  }
  method M() {
    var a, b;
    Syn.MyMethod(b, a);
    a, b := 25, true;
  }
  method P() {
    var d := SynonymLibrary.S.Cons(50, SynonymLibrary.Dt.Nil);
    var e := SynonymLibrary.Dt.Cons(true, SynonymLibrary.S.Cons(40, d));
    var f := SynonymLibrary.S.Cons(50, SynonymLibrary.S.Cons(true, d));  // error: 'true' argument is expected to be an integer
  }
}

module SynonymLibrary {
  type S = Dt<int>
  datatype Dt<T> = Nil | Cons(T, S)
}

module QualifiedDatatypeCtor {
  type S = Dt<int>
  datatype Dt<T> = Nil | Cons(T, S)

  method P() {
    var d: S;
    var f := S.Cons(50, S.Nil);
  }
}

module IntegerBasedValues {
  type T

  newtype Even = x | x % 2 == 0

  method Arrays(n: nat, o: Even, i: Even) returns (x: T)
    requires 0 <= o;
    requires -1 < i;  // note, -1 is not of type Even, but that's for the verifier (not type checker) to check
  {
    var a := new T[n];
    var b := new T[o];
    var m := new T[o, n];
    var m' := new T[o, n, 3.14];  // error: 3.14 is not of an integer-based type
    if {
      case i < n =>  // error: i and n are of different integer-based types
        x := a[i];
      case i < a.Length =>  // error: i and a.Length are of different integer-based types
        x := a[i];
      case i < o =>
        x := b[i];
      case i < b.Length =>  // error: i and b.Length are of different integer-based types
        x := b[i];
      case i < m.Length0 =>  // error: i and m.Length0 are of different integer-based types
      case i < m.Length1 =>  // error: i and m.Length1 are of different integer-based types
    }
  }
  
  method Sequences(a: seq<T>, i: Even) returns (x: T, b: seq<T>)
    requires 0 <= i;
  {
    if {
      case i < |a| =>  // error: i and |a| are of different integer-based types
        x := a[i];
      case i <= int(|a|) =>  // error: type of i is a different integer-based type than int
        b := a[0..i];
    }
  }

  method MultisetUpdate<U>(m: multiset<U>, t: U, n: Even)
  {
    var m' := m[t := n];
    m' := m[t := 12];
    m' := m[t := 1.1415];  // error: real is not an integer-based numeric type
  }
}
