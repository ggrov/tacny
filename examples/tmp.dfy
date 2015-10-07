// RUN: %dafny /compile:0 /rprint:"%t.rprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// This file is a Dafny encoding of chapter 3 from "Concrete Semantics: With Isabelle/HOL" by
// Tobias Nipkow and Gerwin Klein.

// ----- lists -----

datatype List<T> = Nil | Cons(head: T, tail: List<T>)

function append(xs: List, ys: List): List
{
  match xs
  case Nil => ys
  case Cons(x, tail) => Cons(x, append(tail, ys))
}

// ----- arithmetic expressions -----

type vname = string  // variable names
datatype aexp = N(n: int) | V(x: vname) | Plus(0: aexp, 1: aexp)  // arithmetic expressions

type val = int
type state = vname -> val
// In Dafny, functions can in general read the heap (which is not interesting to these examples--in fact, for
// the examples in this file, the fact that functions can read the state is just a distraction, so you can
// just ignore all the lines "reads s.reads" if you prefer) and may have preconditions (that is, the function
// may have some domain that is not specific than what its type says).
// The following predicate holds for a given s if s can be applied to any vname
predicate Total(s: state)
  reads s.reads  // this says that Total(s) can read anything that s can (on any input)
{
  // the following line is the conjunction, over all x, of the precondition of the call s(x)
  forall x :: s.requires(x)
}

function aval(a: aexp, s: state): val
  reads s.reads
  requires Total(s)
{
  match a
  case N(n) => n
  case V(x) => s(x)
  case Plus(a0, a1) => aval(a0, s) + aval(a1, s)
}

// ----- constant folding -----

function asimp_const(a: aexp): aexp
{
  match a
  case N(n) => a
  case V(x) => a
  case Plus(a0, a1) =>
    var as0, as1 := asimp_const(a0), asimp_const(a1);
    if as0.N? && as1.N? then
      N(as0.n + as1.n)
    else
      Plus(as0, as1)
}

lemma AsimpConst(a: aexp, s: state)
  requires Total(s)
  ensures aval(asimp_const(a), s) == aval(a, s)
{
  // by induction
  forall a' | a' < a {
    AsimpConst(a', s);  // this invokes the induction hypothesis for every a' that is structurally smaller than a
  }
  //Here is an alternative proof.  In the first two cases, the proof is trivial.  The Plus case uses two invocations
  //  of the induction hypothesis.
  match a
  case N(n) =>
  case V(x) =>
  case Plus(a0, a1) =>
    AsimpConst(a0, s);
    AsimpConst(a1, s);

}

// more constant folding

function plus(a0: aexp, a1: aexp): aexp
{
  if a0.N? && a1.N? then
    N(a0.n + a1.n)
  else if a0.N? then
    if a0.n == 0 then a1 else Plus(a0, a1)
  else if a1.N? then
    if a1.n == 0 then a0 else Plus(a0, a1)
  else
    Plus(a0, a1)
}

function asimp(a: aexp): aexp
{
  match a
  case N(n) => a
  case V(x) => a
  case Plus(a0, a1) => plus(asimp(a0), asimp(a1))
}

lemma AsimpCorrect(a: aexp, s: state)
  requires Total(s)
  ensures aval(asimp(a), s) == aval(a, s)
{
  // call the induction hypothesis on every value a' that is structurally smaller than a
  forall a' | a' < a { AsimpCorrect(a', s); }
}

// ----- boolean expressions -----

datatype bexp = Bc(v: bool) | Not(op: bexp) | And(b0: bexp, b1: bexp) | Less(a0: aexp, a1: aexp)

function bval(b: bexp, s: state): bool
  reads s.reads
  requires Total(s)
{
  match b
  case Bc(v) => v
  case Not(b) => !bval(b, s)
  case And(b0, b1) => bval(b0, s) && bval(b1, s)
  case Less(a0, a1) => aval(a0, s) < aval(a1, s)
}

// constant folding for booleans

function not(b: bexp): bexp
{
  match b
  case Bc(b0) => Bc(!b0)
  case Not(b0) => b0  // this case is not in the Nipkow and Klein book, but it seems a nice one to include
  case And(_, _) => Not(b)
  case Less(_, _) => Not(b)
}

function and(b0: bexp, b1: bexp): bexp
{
  if b0.Bc? then
    if b0.v then b1 else b0
  else if b1.Bc? then
    if b1.v then b0 else b1
  else
    And(b0, b1)
}

function less(a0: aexp, a1: aexp): bexp
{
  if a0.N? && a1.N? then
    Bc(a0.n < a1.n)
  else
    Less(a0, a1)
}

function bsimp(b: bexp): bexp
{
  match b
  case Bc(v) => b
  case Not(b0) => not(bsimp(b0))
  case And(b0, b1) => and(bsimp(b0), bsimp(b1))
  case Less(a0, a1) => less(asimp(a0), asimp(a1))
}

// TACNY: we should be able to do this one!

lemma BsimpCorrect(b: bexp, s: state)
  requires Total(s)
  ensures bval(bsimp(b), s) == bval(b, s)
{
  match b
  case Bc(v) =>
  case Not(b0) =>
    BsimpCorrect(b0, s);
  case And(b0, b1) =>
    BsimpCorrect(b0, s); BsimpCorrect(b1, s);
  case Less(a0, a1) =>
    AsimpCorrect(a1, s); AsimpCorrect(a0, s);
}
