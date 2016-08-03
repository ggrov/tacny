// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module TestModule {
  class TestClass {
    copredicate P(b: bool)
    {
      !b && Q(null)
    }

    copredicate Q(a: array<int>)
    {
      a == null && P(true)
    }

    copredicate S(d: set<int>)
    {
      this.Undeclared#[5](d) &&  // error: 'Undeclared#' is undeclared
      Undeclared#[5](d) &&  // error: 'Undeclared#' is undeclared
      this.S#[5](d) &&
      S#[5](d) &&
      S#[_k](d)  // error: _k is not an identifier in scope
    }

    colemma CM(d: set<int>)
    {
      var b;
      b := this.S#[5](d);
      b := S#[5](d);
      this.CM#[5](d);
      CM#[5](d);
    }
  }
}

module GhostCheck0 {
  codatatype Stream<G> = Cons(head: G, tail: Stream)
  method UseStream0(s: Stream)
  {
    var x := 3;
    if (s == s.tail) {  // error: this operation is allowed only in ghost contexts
      x := x + 2;
    }
  }
}
module GhostCheck1 {
  codatatype Stream<G> = Cons(head: G, tail: Stream)
  method UseStream1(s: Stream)
  {
    var x := 3;
    if (s ==#[20] s.tail) {  // this seems innocent enough, but it's currently not supported by the compiler, so...
      x := x + 7;  // error: therefore, this is an error
    }
  }
}
module GhostCheck2 {
  codatatype Stream<G> = Cons(head: G, tail: Stream)
  ghost method UseStreamGhost(s: Stream)
  {
    var x := 3;
    if (s == s.tail) {  // fine
      x := x + 2;
    }
  }
}

module Mojul0 {
  class MyClass {
    copredicate D()
      reads this;  // error: copredicates are not allowed to have a reads clause -- WHY NOT?
    {
      true
    }

    copredicate NoEnsuresPlease(m: nat)
      ensures NoEnsuresPlease(m) ==> m < 100;  // error: a copredicate is not allowed to have an 'ensures' clause
    {
      m < 75
    }

    // Note, 'decreases' clauses are also disallowed on copredicates, but the parser takes care of that
  }
}

module Mojul1 {
  copredicate A() { B() }  // error: SCC of a copredicate must include only copredicates
  predicate B() { A() }

  copredicate X() { Y() }
  copredicate Y() { X#[10]() }  // error: X is not allowed to depend on X#

  colemma M()
  {
    N();
  }
  colemma N()
  {
    Z();
    W();  // error: not allowed to make co-recursive call to non-colemma
  }
  ghost method Z() { }
  ghost method W() { M(); }

  colemma G() { H(); }
  colemma H() { G#[10](); }  // fine for colemma/prefix-lemma
}

module CallGraph {
  // colemma -> copredicate -> colemma
  // colemma -> copredicate -> prefix lemma
  colemma CoLemma(n: nat)
  {
    var q := Q(n);  // error
    var r := R(n);  // error
  }

  copredicate Q(n: nat)
  {
    calc { 87; { CoLemma(n); } }  // error: this recursive call not allowed
    false
  }

  copredicate R(n: nat)
  {
    calc { 87; { CoLemma#[n](n); } }  // error: this recursive call not allowed
    false
  }

  // colemma -> prefix predicate -> colemma
  // colemma -> prefix predicate -> prefix lemma
  colemma CoLemma_D(n: nat)
  {
    var q := Q_D#[n](n);  // error
    var r := R_D#[n](n);  // error
  }

  copredicate Q_D(n: nat)
  {
    calc { 88; { CoLemma_D(n); } }  // error: this recursive call not allowed
    false
  }

  copredicate R_D(n: nat)
  {
    calc { 89; { CoLemma_D#[n](n); } }  // error: this recursive call not allowed
    false
  }

  // copredicate -> function -> copredicate
  // copredicate -> function -> prefix predicate
  copredicate P(n: nat)
  {
    G0(n)  // error
    <
    G1(n)  // error
  }

  function G0(n: nat): int
  {
    calc { true; { assert P(n); } }
    100
  }
  function G1(n: nat): int
  {
    calc { true; { assert P#[n](n); } }
    101
  }

  colemma J()
  {
    var f := JF();  // error: cannot call non-colemma recursively from colemma
  }
  function JF(): int
  {
    J();
    5
  }
}

module CrashRegression {
  codatatype Stream = Cons(int, Stream)

  // The following functions (where A ends up being the representative in the
  // SCC and B, which is also in the same SCC, has no body) once crashed the
  // resolver.
  function A(): Stream
  {
    B()
  }
  function B(): Stream
    ensures A() == S();

  function S(): Stream
}

module AmbiguousTypeParameters {
  codatatype Stream<T> = Cons(T, Stream)

  function A(): Stream
  {
    B()
  }

  // Here, the type arguments to A and S cannot be resolved
  function B(): Stream
    ensures A() == S();

  function S(): Stream
}

