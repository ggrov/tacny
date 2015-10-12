codatatype Stream<T> = SNil | SCons(head: T, tail: Stream<T>)

function Up(n: int): Stream<int>
{
  SCons(n, Up(n+1))
}

function FivesUp(n: int): Stream<int>
  decreases 4 - (n-1) % 5;
{
  if n % 5 == 0 then SCons(n, FivesUp(n+1))
  else FivesUp(n+1)
}

copredicate Pos(s: Stream<int>)
{
  match s
  case SNil => true
  case SCons(x, rest) => x > 0 && Pos(rest)
}

function SAppend(xs: Stream, ys: Stream): Stream
{
  match xs
  case SNil => ys
  case SCons(x, rest) => SCons(x, SAppend(rest, ys))
}

lemma {:induction false} SAppendIsAssociativeK(k:nat, a:Stream, b:Stream, c:Stream)
  ensures SAppend(SAppend(a, b), c) ==#[k] SAppend(a, SAppend(b, c));
  decreases k;
{
  match (a) {
  case SNil =>
  case SCons(h, t) => if (k > 0) { SAppendIsAssociativeK(k - 1, t, b, c); }
  }
}

lemma SAppendIsAssociative(a:Stream, b:Stream, c:Stream)
  ensures SAppend(SAppend(a, b), c) == SAppend(a, SAppend(b, c));
{
  forall k:nat { SAppendIsAssociativeK(k, a, b, c); }
  assert (forall k:nat :: SAppend(SAppend(a, b), c) ==#[k] SAppend(a, SAppend(b, c)));
}

colemma {:induction false} SAppendIsAssociativeC(a:Stream, b:Stream, c:Stream)
  ensures SAppend(SAppend(a, b), c) == SAppend(a, SAppend(b, c));
{
    mytac(a);
}

tactic mytac(a : Element)
{
  cases(a){
    var l :| l in lemmas();
    var v := merge(variables(), params());
    perm(l, v);
  }
}