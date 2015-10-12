// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
codatatype Stream<X> = Nil | Cons(head: X, tail: Stream)

type X

class _default {
  function append(M: Stream, N: Stream): Stream
  {
    match M
    case Nil =>
      N
    case Cons(t, M') =>
      Cons(t, append(M', N))
  }

  function f(x: X): X

  function g(x: X): X

  function map_f(M: Stream<X>): Stream<X>
  {
    match M
    case Nil =>
      Nil
    case Cons(x, N) =>
      Cons(f(x), map_f(N))
  }

  function map_g(M: Stream<X>): Stream<X>
  {
    match M
    case Nil =>
      Nil
    case Cons(x, N) =>
      Cons(g(x), map_g(N))
  }

  function map_fg(M: Stream<X>): Stream<X>
  {
    match M
    case Nil =>
      Nil
    case Cons(x, N) =>
      Cons(f(g(x)), map_fg(N))
  }

  colemma Theorem0(M: Stream<X>)
    ensures map_fg(M) == map_f(map_g(M))
  {
    match M
    case Nil =>
    case Cons(head, tail) =>
  }

  colemma Theorem1(M: Stream<X>, N: Stream<X>)
    ensures map_f(append(M, N)) == append(map_f(M), map_f(N))
  {
    match M
    case Nil =>
    case Cons(head, tail) =>
  }

  lemma Theorem2(M: Stream<X>)
    ensures append(Nil, M) == M
  {
  }

  colemma Theorem3(M: Stream<X>)
    ensures append(M, Nil) == M
  {
    match M
    case Nil =>
    case Cons(head, tail) =>
  }

  colemma Theorem4(M: Stream<X>, N: Stream<X>, P: Stream<X>)
    ensures append(M, append(N, P)) == append(append(M, N), P)
  {
    match M
    case Nil =>
    case Cons(head, tail) =>
  }

  function FlattenStartMarker<T>(M: Stream<Stream>, startMarker: T): Stream
  {
    PrependThenFlattenStartMarker(Nil, M, startMarker)
  }

  function PrependThenFlattenStartMarker<T>(prefix: Stream, M: Stream<Stream>, startMarker: T): Stream
  {
    match prefix
    case Cons(hd, tl) =>
      Cons(hd, PrependThenFlattenStartMarker(tl, M, startMarker))
    case Nil =>
      match M
      case Nil =>
        Nil
      case Cons(s, N) =>
        Cons(startMarker, PrependThenFlattenStartMarker(s, N, startMarker))
  }

  copredicate StreamOfNonEmpties(M: Stream<Stream>): bool
  {
    match M
    case Nil =>
      true
    case Cons(s, N) =>
      s.Cons? &&
      StreamOfNonEmpties(N)
  }

  function FlattenNonEmpties(M: Stream<Stream>): Stream
    requires StreamOfNonEmpties(M)
  {
    PrependThenFlattenNonEmpties(Nil, M)
  }

  function PrependThenFlattenNonEmpties(prefix: Stream, M: Stream<Stream>): Stream
    requires StreamOfNonEmpties(M)
  {
    match prefix
    case Cons(hd, tl) =>
      Cons(hd, PrependThenFlattenNonEmpties(tl, M))
    case Nil =>
      match M
      case Nil =>
        Nil
      case Cons(s, N) =>
        Cons(s.head, PrependThenFlattenNonEmpties(s.tail, N))
  }

  function Prepend<T>(x: T, M: Stream<Stream>): Stream<Stream>
  {
    match M
    case Nil =>
      Nil
    case Cons(s, N) =>
      Cons(Cons(x, s), Prepend(x, N))
  }

  colemma Prepend_Lemma<T>(x: T, M: Stream<Stream>)
    ensures StreamOfNonEmpties(Prepend(x, M))
  {
    match M
    case Nil =>
    case Cons(head, tail) =>
  }

  lemma Theorem_Flatten<T>(M: Stream<Stream>, startMarker: T)
    ensures StreamOfNonEmpties(Prepend(startMarker, M)) ==> FlattenStartMarker(M, startMarker) == FlattenNonEmpties(Prepend(startMarker, M))
  {
    Prepend_Lemma(startMarker, M);
    Lemma_Flatten(Nil, M, startMarker);
  }

  colemma Lemma_Flatten<T>(prefix: Stream, M: Stream<Stream>, startMarker: T)
    ensures StreamOfNonEmpties(Prepend(startMarker, M)) ==> PrependThenFlattenStartMarker(prefix, M, startMarker) == PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, M))
  {
    Prepend_Lemma(startMarker, M);
    match prefix {
      case Cons(hd, tl) =>
        Lemma_Flatten(tl, M, startMarker);
      case Nil =>
        match M {
          case Nil =>
          case Cons(s, N) =>
            if * {
              Lemma_Flatten(s, N, startMarker);
            } else {
              calc {
                PrependThenFlattenStartMarker(prefix, M, startMarker);
              ==
                Cons(startMarker, PrependThenFlattenStartMarker(s, N, startMarker));
              }
              calc {
                PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, M));
              ==
                PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, Cons(s, N)));
              ==
                PrependThenFlattenNonEmpties(prefix, Cons(Cons(startMarker, s), Prepend(startMarker, N)));
              ==
                Cons(Cons(startMarker, s).head, PrependThenFlattenNonEmpties(Cons(startMarker, s).tail, Prepend(startMarker, N)));
              ==
                Cons(startMarker, PrependThenFlattenNonEmpties(s, Prepend(startMarker, N)));
              }
              calc {
                PrependThenFlattenStartMarker(prefix, M, startMarker) ==#[_k] PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, M));
                {
                  assert PrependThenFlattenStartMarker(prefix, M, startMarker) == Cons(startMarker, PrependThenFlattenStartMarker(s, N, startMarker));
                }
                Cons(startMarker, PrependThenFlattenStartMarker(s, N, startMarker)) ==#[_k] PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, M));
                {
                  assert PrependThenFlattenNonEmpties(prefix, Prepend(startMarker, M)) == Cons(startMarker, PrependThenFlattenNonEmpties(s, Prepend(startMarker, N)));
                }
                Cons(startMarker, PrependThenFlattenStartMarker(s, N, startMarker)) ==#[_k] Cons(startMarker, PrependThenFlattenNonEmpties(s, Prepend(startMarker, N)));
              ==
                startMarker == startMarker &&
                PrependThenFlattenStartMarker(s, N, startMarker) ==#[_k - 1] PrependThenFlattenNonEmpties(s, Prepend(startMarker, N));
                {
                  Lemma_Flatten(s, N, startMarker);
                  assert PrependThenFlattenStartMarker(s, N, startMarker) ==#[_k - 1] PrependThenFlattenNonEmpties(s, Prepend(startMarker, N));
                }
                true;
              }
            }
        }
    }
  }

  colemma Lemma_FlattenAppend0<T>(s: Stream, M: Stream<Stream>, startMarker: T)
    ensures PrependThenFlattenStartMarker(s, M, startMarker) == append(s, PrependThenFlattenStartMarker(Nil, M, startMarker))
  {
    match s
    case Nil =>
    case Cons(head, tail) =>
  }

  colemma Lemma_FlattenAppend1<T>(s: Stream, M: Stream<Stream>)
    requires StreamOfNonEmpties(M)
    ensures PrependThenFlattenNonEmpties(s, M) == append(s, PrependThenFlattenNonEmpties(Nil, M))
  {
    match s
    case Nil =>
    case Cons(head, tail) =>
  }
}