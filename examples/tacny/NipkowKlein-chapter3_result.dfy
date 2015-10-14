// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
datatype List<T> = Nil | Cons(head: T, tail: List<T>)

type vname = string

datatype aexp = N(n: int) | V(x: vname) | Plus(a0: aexp, a1: aexp)

type val = int

type state = vname -> val

datatype bexp = Bc(v: bool) | Not(op: bexp) | And(a0: bexp, a1: bexp) | Less(l0: aexp, l1: aexp)

class _default {
  function append(xs: List, ys: List): List
  {
    match xs
    case Nil =>
      ys
    case Cons(x, tail) =>
      Cons(x, append(tail, ys))
  }

  predicate Total(s: state)
    reads s.reads
  {
    forall x :: 
      s.requires(x)
  }

  function aval(a: aexp, s: state): val
    requires Total(s)
    reads s.reads
  {
    match a
    case N(n) =>
      n
    case V(x) =>
      s(x)
    case Plus(a0, a1) =>
      aval(a0, s) + aval(a1, s)
  }

  function asimp_const(a: aexp): aexp
  {
    match a
    case N(n) =>
      a
    case V(x) =>
      a
    case Plus(a0, a1) =>
      var as0, as1 := asimp_const(a0), asimp_const(a1);
      if as0.N? && as1.N? then
        N(as0.n + as1.n)
      else
        Plus(as0, as1)
  }

  function plus(a0: aexp, a1: aexp): aexp
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
  {
    match a
    case N(n) =>
      a
    case V(x) =>
      a
    case Plus(a0, a1) =>
      plus(asimp(a0), asimp(a1))
  }

  lemma AsimpCorrect(a: aexp, s: state)
    requires Total(s)
    ensures aval(asimp(a), s) == aval(a, s)
  {
    forall a' | a' < a {
      AsimpCorrect(a', s);
    }
  }

  lemma AsimpConst(a: aexp, s: state)
    requires Total(s)
    ensures aval(asimp_const(a), s) == aval(a, s)
  {
    match a
    case N(n) =>
    case V(x) =>
    case Plus(a0, a1) =>
      AsimpConst(a0, s);
      AsimpConst(a1, s);
  }

  function bval(b: bexp, s: state): bool
    requires Total(s)
    reads s.reads
  {
    match b
    case Bc(v) =>
      v
    case Not(b) =>
      !bval(b, s)
    case And(b0, b1) =>
      bval(b0, s) &&
      bval(b1, s)
    case Less(a0, a1) =>
      aval(a0, s) < aval(a1, s)
  }

  function not(b: bexp): bexp
  {
    match b
    case Bc(b0) =>
      Bc(!b0)
    case Not(b0) =>
      b0
    case And(_, _) =>
      Not(b)
    case Less(_, _) =>
      Not(b)
  }

  function and(b0: bexp, b1: bexp): bexp
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
  {
    if a0.N? && a1.N? then
      Bc(a0.n < a1.n)
    else
      Less(a0, a1)
  }

  function bsimp(b: bexp): bexp
  {
    match b
    case Bc(v) =>
      b
    case Not(b0) =>
      not(bsimp(b0))
    case And(b0, b1) =>
      and(bsimp(b0), bsimp(b1))
    case Less(a0, a1) =>
      less(asimp(a0), asimp(a1))
  }

  lemma BsimpCorrect(b: bexp, s: state)
    requires Total(s)
    ensures bval(bsimp(b), s) == bval(b, s)
  {
    match b
    case Bc(v) =>
    case Not(op) =>
      BsimpCorrect(op, s);
    case And(a0, a1) =>
      BsimpCorrect(a0, s);
      BsimpCorrect(a1, s);
    case Less(l0, l1) =>
      AsimpCorrect(l1, s);
      AsimpCorrect(l0, s);
  }
}