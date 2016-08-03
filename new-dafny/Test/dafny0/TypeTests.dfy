// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class C {
  function F(c: C, d: D): bool { true }
  method M(x: int) returns (y: int, c: C)
    requires F(D.A, this);  // 2 errors
    requires F(4, 5);  // 2 errors
    requires F(this, D.A);  // good
  { }

  method Caller()
  {
    var m,n := M(true);  // error on in-parameter
    n,m := M(m);  // 2 errors on out-parameters
  }
}

datatype D = A

datatype NeverendingList = Cons(int, NeverendingList)  // error: no grounding constructor

datatype MutuallyRecursiveDataType<T> =
  FromANumber(int) |  // this is the base case
  Else(TheCounterpart<T>, C)

datatype TheCounterpart<T> =
  TreeLike(TheCounterpart<T>, TheCounterpart<T>) |
  More(MutuallyRecursiveDataType<T>)

// these 'ReverseOrder_' order tests may be called white-box unit tests
datatype ReverseOrder_MutuallyRecursiveDataType<T> =
  FromANumber(int) |  // this is the base case
  Else(ReverseOrder_TheCounterpart<T>, C)

datatype ReverseOrder_TheCounterpart<T> =
  TreeLike(ReverseOrder_TheCounterpart<T>, ReverseOrder_TheCounterpart<T>) |
  More(ReverseOrder_MutuallyRecursiveDataType<T>)

// ---------------------

module ArrayTests {
  ghost method G(a: array<int>)
    requires a != null && 10 <= a.Length;
    modifies a;
  {
    a[7] := 13;  // error: array elements are not ghost locations
  }
}

// ---------------------

method DuplicateVarName(x: int) returns (y: int)
{
  var z: int;
  var z: int;  // error: redeclaration of local
  var x := x;  // redeclaration of in-parameter is fine
  var x := x;  // error: but a redeclaration of that local is not fine
  {
    var x := x;  // an inner local variable of the same name is fine
    var x := x;  // error: but a redeclaration thereof is not okay
    var y := y;  // duplicating an out-parameter here is fine
  }
  var y := y;  // error: redeclaration of an out-parameter is not allowed (it is
               // treated like an outermost-scoped local in this regard)
}

method ScopeTests()
{
  var x := x;  // error: the 'x' in the RHS is not in scope
  var y: real :| y == y;  // fine, 'y' is in scope in the RHS
  var z := DuplicateVarName(z);  // error: the 'z' in the RHS is not in scope
  var w0, w1 := IntTransform(w1), IntTransform(w0);  // errors two
  var c := new MyClass.Init(null);  // fine
  var d := new MyClass.Init(c);  // fine
  var e := new MyClass.Init(e);  // error: the 'e' in the RHS is not in scope
  e := new MyClass.Init(e);  // fine (no variable is being introduced here)
  e.c := new MyClass.Init(e);  // also fine
}

function IntTransform(w: int): int

class MyClass {
  var c: MyClass;
  constructor Init(c: MyClass)
}

// ---------------------

method InitCalls() {
  var c := new C.F(null, null);  // error: F is not a method
  var d := new C.M(8);  // error: M has out parameters
  var e := new C.Caller();
}

// ---------------------

method ArrayRangeAssignments(a: array<C>, c: C)
  requires a != null && 10 <= a.Length;
{
  a[0..5] := new C;  // error: this is not allowed
  a[1..4] := *;  // error: this is not allowed
  a[2..3] := c;  // error: this is not allowed
  var s: seq<C> := [null,null,null,null,null];
  s[0..5] := new C;  // error: this is not allowed
  s[1..4] := *;  // error: this is not allowed
  s[2..3] := c;  // error: this is not allowed
}

// --------------------- tests of restrictions on subranges (nat)

method K() {
  var s: set<nat>;  // error: not allowed to instantiate 'set' with 'nat'
  var d: MutuallyRecursiveDataType<nat>;  // error: not allowed to instantiate with 'nat'
  var a := new nat[100];  // error: not allowed the type array<nat>
  var b := new nat[100,200];  // error: not allowed the type array2<nat>

  // constructors
  var ci0 := new Expl_Class.Init<nat>(0, 0);  // error: subrange not allowed here
  var ci1 := new Expl_Class<nat>;  // error
  var ci2 := new Expl_Class<nat>.Init(0, 0);  // error

  // collection types (sets are above) and array types
  var m0: multiset<nat>;  // error
  var m1: seq<nat>;  // error
  var m2: map<nat,int>;  // error
  var m3: map<int,nat>;  // error
  var n: seq<MutuallyRecursiveDataType<nat>>;  // error
  var o: array<nat>;  // error
  var o': array2<nat>;  // error

  // tuple types
  var tu0: (nat);  // no problem, this just means 'nat'
  var tu1: (nat,int);  // error
  var tu2: (int,nat);  // error

  // function types
  var fn: nat -> int;  // error
  var gn: int -> nat;  // error
  var hn: (int,nat) -> int;  // error

  // the following tests test NameSegment and ExprDotName in types:
  var k: Expl_Class<nat>;  // error
  var k': Expl_Module.E<nat>;  // error

  // the following tests test NameSegment and ExprDotName in expressions:
  var e0 := Expl_M<nat>(0);  // error
  var e1 := Expl_F<nat>(0);  // error
  var ec := new Expl_Class<int>;
  ec.Init<nat>(0, 0);  // error
  Expl_Class.DoIt<nat>(0, 0);  // error
  Expl_Class<nat>.DoIt(0, 0);  // error
  Expl_Module.E.N<nat>(0, 0);  // error
  Expl_Module.E<nat>.N(0, 0);  // error
}
method Expl_M<T>(x: T) returns (y: T)
function method Expl_F<T>(x: T): T
class Expl_Class<T> {
  method Init<U>(t: T, u: U)
  static method DoIt<U>(t: T, u: U)
}
module Expl_Module {
  class E<T> {
    static method N<U>(t: T, u: U)
  }
}

// --------------------- more ghost tests, for assign-such-that statements

module MoreGhostTests {
  method M()
  {
    ghost var b: bool;
    ghost var k: int, l: int;
    var m: int;

    k :| k < 10;
    k, m :| 0 <= k < m;  // error: LHS has non-ghost and RHS has ghost
    m :| m < 10;

    // Because of the ghost guard, these 'if' statements are ghost contexts, so only
    // assignments to ghosts are allowed.
    if (b) {
      k :| k < 10;  // should be allowed
      k, l :| 0 <= k < l;  // ditto
    }
    if (b) {
      m :| m < 10;  // error: not allowed in ghost context
      k, m :| 0 <= k < m;  // error: not allowed in ghost context
    }
  }

  ghost method GhostM() returns (x: int)
  {
    x :| true;  // no problem (but there once was a problem with this case, where an error was generated for no reason)
  }
}

// ------------------ cycles that could arise from proxy assignments ---------

module ProxyCycles {
  datatype Dt<X> = Ctor(X -> Dt<X>)
  method M0()
  {
    var dt: Dt<int>;
    var f := x => x;
    dt := Ctor(f);  // error: cannot infer a type for f
  }
  method M1()
  {
    var dt: Dt;
    var f := x => x;
    dt := Ctor(f);  // error: cannot infer a type for f
  }

  function method F<X>(x: X): set<X>
  method N()
  {
    var x;
    x := F(x);  // error: cannot infer type for x
  }
}
