// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
codatatype Stream<T> = SNil | SCons(head: T, tail: Stream<T>)

class _default {
  function Up(n: int): Stream<int>
    decreases 1, n
  {
    SCons(n, Up(n + 1))
  }

  function FivesUp(n: int): Stream<int>
    decreases 1, 4 - (n - 1) % 5
  {
    if n % 5 == 0 then
      SCons(n, FivesUp(n + 1))
    else
      FivesUp(n + 1)
  }

  copredicate Pos(s: Stream<int>): bool
  {
    match s
    case SNil =>
      true
    case SCons(x: int, rest: Stream<int>) =>
      x > 0 &&
      Pos(rest)
  }
  /***
  predicate Pos#[_k: nat](s: Stream<int>)
    decreases _k
  {
    match s
    case SNil =>
      true
    case SCons(x: int, rest: Stream<int>) =>
      x > 0 &&
      Pos(rest)
  }
  ***/

  function SAppend<_T0>(xs: Stream<_T0>, ys: Stream<_T0>): Stream<_T0>
    decreases 1
  {
    match xs
    case SNil =>
      ys
    case SCons(x: _T0, rest: Stream<_T0>) =>
      SCons(x, SAppend(rest, ys))
  }

  lemma {:induction false} SAppendIsAssociativeK<_T0>(k: nat, a: Stream<_T0>, b: Stream<_T0>, c: Stream<_T0>)
    ensures SAppend(SAppend(a, b), c) ==#[k] SAppend(a, SAppend(b, c))
    decreases k
  {
    match a {
      case SNil =>
      case SCons(h: _T0, t: Stream<_T0>) =>
        if k > 0 {
          SAppendIsAssociativeK(k - 1, t, b, c);
        }
    }
  }

  lemma SAppendIsAssociative<_T0>(a: Stream<_T0>, b: Stream<_T0>, c: Stream<_T0>)
    ensures SAppend(SAppend(a, b), c) == SAppend(a, SAppend(b, c))
  {
    forall k: nat | true {
      SAppendIsAssociativeK(k, a, b, c);
    }
    assert forall k: nat :: SAppend(SAppend(a, b), c) ==#[k] SAppend(a, SAppend(b, c));
  }

  colemma {:induction false} SAppendIsAssociativeC<_T0>(a: Stream<_T0>, b: Stream<_T0>, c: Stream<_T0>)
    ensures SAppend(SAppend(a, b), c) == SAppend(a, SAppend(b, c))
  {
    match a
    case SNil =>
    case SCons(head: _T0, tail: Stream<_T0>) =>
      SAppendIsAssociative(tail, b, c);
  }
}