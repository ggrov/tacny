// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
datatype List<T> = Nil | Cons(head: T, tail: List<T>)

type vname = string

datatype aexp = N(n: int) | V(x: vname) | Plus(0: aexp, 1: aexp)

type val = int

type state = vname -> val

datatype bexp = Bc(v: bool) | Not(op: bexp) | And(0: bexp, 1: bexp) | Less(a0: aexp, a1: aexp)

class _default {
  function append<_T0>(xs: List<_T0>, ys: List<_T0>): List<_T0>
    decreases xs, ys
  {
    match xs
    case Nil =>
      ys
    case Cons(x: _T0, tail: List<_T0>) =>
      Cons(x, append(tail, ys))
  }

  predicate Total(s: state)
    reads s.reads
    decreases set _x0: vname, _o0: object | _o0 in s.reads(_x0) :: _o0
  {
    forall x: seq<char> :: 
      s.requires(x)
  }

  function aval(a: aexp, s: state): val
    requires Total(s)
    reads s.reads
    decreases set _x0: vname, _o0: object | _o0 in s.reads(_x0) :: _o0, a
  {
    match a
    case N(n: int) =>
      n
    case V(x: seq<char>) =>
      s(x)
    case Plus(a0: aexp, a1: aexp) =>
      aval(a0, s) + aval(a1, s)
  }

  function asimp_const(a: aexp): aexp
    decreases a
  {
    match a
    case N(n: int) =>
      a
    case V(x: seq<char>) =>
      a
    case Plus(a0: aexp, a1: aexp) =>
      var as0: aexp, as1: aexp := asimp_const(a0), asimp_const(a1);
      if as0.N? && as1.N? then
        N(as0.n + as1.n)
      else
        Plus(as0, as1)
  }

  function plus(a0: aexp, a1: aexp): aexp
    decreases a0, a1
  {
    if a0.N? && a1.N? then
      N(a0.n + a1.n)
    else if a0.N? then
      if a0.n == 0 then
        a1
      else
        Plus(a0, a1)
    else if a1.N? then
      if a1.n == 0 then
        a0
      else
        Plus(a0, a1)
    else
      Plus(a0, a1)
  }

  function asimp(a: aexp): aexp
    decreases a
  {
    match a
    case N(n: int) =>
      a
    case V(x: seq<char>) =>
      a
    case Plus(a0: aexp, a1: aexp) =>
      plus(asimp(a0), asimp(a1))
  }

  lemma AsimpCorrect(a: aexp, s: state)
    requires Total(s)
    ensures aval(asimp(a), s) == aval(a, s)
    decreases a
  {
    forall a': aexp | a' < a {
      AsimpCorrect(a', s);
    }
  }

  function bval(b: bexp, s: state): bool
    requires Total(s)
    reads s.reads
    decreases set _x0: vname, _o0: object | _o0 in s.reads(_x0) :: _o0, b
  {
    match b
    case Bc(v: bool) =>
      v
    case Not(b: bexp) =>
      !bval(b, s)
    case And(b0: bexp, b1: bexp) =>
      bval(b0, s) &&
      bval(b1, s)
    case Less(a0: aexp, a1: aexp) =>
      aval(a0, s) < aval(a1, s)
  }

  function not(b: bexp): bexp
    decreases b
  {
    match b
    case Bc(b0: bool) =>
      Bc(!b0)
    case Not(b0: bexp) =>
      b0
    case And(_: bexp, _: bexp) =>
      Not(b)
    case Less(_: aexp, _: aexp) =>
      Not(b)
  }

  function and(b0: bexp, b1: bexp): bexp
    decreases b0, b1
  {
    if b0.Bc? then
      if b0.v then
        b1
      else
        b0
    else if b1.Bc? then
      if b1.v then
        b0
      else
        b1
    else
      And(b0, b1)
  }

  function less(a0: aexp, a1: aexp): bexp
    decreases a0, a1
  {
    if a0.N? && a1.N? then
      Bc(a0.n < a1.n)
    else
      Less(a0, a1)
  }

  function bsimp(b: bexp): bexp
    decreases b
  {
    match b
    case Bc(v: bool) =>
      b
    case Not(b0: bexp) =>
      not(bsimp(b0))
    case And(b0: bexp, b1: bexp) =>
      and(bsimp(b0), bsimp(b1))
    case Less(a0: aexp, a1: aexp) =>
      less(asimp(a0), asimp(a1))
  }

  ghost method BsimpCorrect(b: bexp, s: state)
    requires Total(s)
    ensures bval(bsimp(b), s) == bval(b, s)
    decreases b
  {
    match b
    case Bc(v: bool) =>
    case Not(op: bexp) =>
      BsimpCorrect(op, s);
    case And(0: bexp, 1: bexp) =>
      BsimpCorrect(0, s);
      BsimpCorrect(1, s);
    case Less(a0: aexp, a1: aexp) =>
      AsimpCorrect(a1, s);
      AsimpCorrect(a0, s);
  }
}
