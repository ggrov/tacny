// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

codatatype Stream<T> = Cons(head: T, tail: Stream)

function Upward(n: int): Stream<int>
{
  Cons(n, Upward(n + 1))
}

copredicate Pos(s: Stream<int>)
{
  0 < s.head && Pos(s.tail)
}
predicate FullPos(s: Stream<int>) { Pos(s) }  // a way in the test file to sidestep focal-predicate rewrites

colemma {:induction false} PosLemma0(n: int)
  requires 1 <= n;
  ensures Pos(Upward(n));
{
  PosLemma0(n + 1);  // this completes the proof
}

colemma {:induction false} PosLemma1(n: int)
  requires 1 <= n;
  ensures Pos(Upward(n));
{
  PosLemma1(n + 1);
  if (*) {
    assert FullPos(Upward(n + 1));  // error: cannot conclude this here, because we only have prefix predicates
  }
}

colemma {:induction false} PosLemma2(n: int)
  requires 1 <= n;
  ensures Pos(Upward(n));
{
  PosLemma2(n + 1);
  if (*) {
    assert Pos(Upward(n + 1));  // Pos gets rewritten to Pos#[_k-1], which does hold
  } else if (*) {
    assert Pos#[_k-1](Upward(n + 1));  // explicitly saying Pos#[_k-1] also holds
  } else if (*) {
    assert Pos#[_k](Upward(n + 1));  // error: this is not known to hold for _k and n+1
  } else if (*) {
    assert Pos#[_k](Upward(n));  // but it does hold with Pos#[_k] and n (which is the postcondition of the prefix lemma)
  } else if (*) {
    assert Pos#[_k+1](Upward(n));  // error: this is too much to ask for
  }
}

colemma {:induction false} Outside_CoLemma_Caller_PosLemma(n: int)
  requires 1 <= n;
  ensures Pos(Upward(n));
{
  assert Upward(n).tail == Upward(n + 1);  // follows from one unrolling of the definition of Upward
  PosLemma0(n + 1);
  assert Pos(Upward(n+1));  // this follows directly from the previous line, because it isn't a recursive call
  // ... and it implies the prefix predicate we're supposed to establish
}

method Outside_Method_Caller_PosLemma(n: int)
  requires 1 <= n;
  ensures Pos(Upward(n));  // this method postcondition follows just fine
{
  assert Upward(n).tail == Upward(n + 1);  // follows from one unrolling of the definition of Upward
  PosLemma0(n + 1);
  assert Pos(Upward(n+1));  // this follows directly from the previous line, because it isn't a recursive call
}

copredicate X(s: Stream)  // this is equivalent to always returning 'true'
{
  X(s)
}

colemma {:induction false} AlwaysLemma_X0(s: Stream)
  ensures X(s);  // prove that X(s) really is always 'true'
{  // error: this is not the right proof
  AlwaysLemma_X0(s.tail);
}

colemma {:induction false} AlwaysLemma_X1(s: Stream)
  ensures X(s);  // prove that X(s) really is always 'true'
{
  AlwaysLemma_X1(s);  // this is the right proof
}
predicate FullX(s: Stream) { X(s) }  // a way in the test file to sidestep focal-predicate rewrites

colemma {:induction false} AlwaysLemma_X2(s: Stream)
  ensures X(s);
{
  AlwaysLemma_X2(s);
  if (*) {
    assert FullX(s);  // error: cannot conclude the full predicate here
  }
}

colemma {:induction false} AlwaysLemma_X3(s: Stream)
  ensures X(s);
{
  AlwaysLemma_X3(s);
  if (*) {
    assert X(s);  // holds, because it gets rewritten to X#[_k-1]
  } else if (*) {
    assert X#[_k-1](s);  // explicitly saying X#[_k-1] also holds
  } else if (*) {
    assert X#[_k](s);  // in fact, X#[_k] holds, too (which is the postcondition of the prefix lemma)
  } else if (*) {
    assert X#[_k+1](s);  // as it turns out, this holds too, since the definition of X makes X#[_k+1] equal X#[_k]
  }
}

copredicate Y(s: Stream)  // this is equivalent to always returning 'true'
{
  Y(s.tail)
}
predicate FullY(s: Stream) { Y(s) }  // a way in the test file to sidestep focal-predicate rewrites

colemma {:induction false} AlwaysLemma_Y0(s: Stream)
  ensures Y(s);  // prove that Y(s) really is always 'true'
{
  AlwaysLemma_Y0(s.tail);  // this is a correct proof
}

colemma {:induction false} AlwaysLemma_Y1(s: Stream)
  ensures Y(s);
{  // error: this is not the right proof
  AlwaysLemma_Y1(s);
}

colemma {:induction false} AlwaysLemma_Y2(s: Stream)
  ensures Y(s);
{
  AlwaysLemma_Y2(s.tail);
  if (*) {
    assert FullY(s.tail);  // error: not provable here
  }
}

colemma {:induction false} AlwaysLemma_Y3(s: Stream)
  ensures Y(s);
{
  AlwaysLemma_Y3(s.tail);
  if (*) {
    assert Y(s.tail);  // this holds, because it's rewritten to Y#[_k-1]
  } else if (*) {
    assert Y#[_k-1](s.tail);
  } else if (*) {
    assert Y#[_k](s.tail);  // error: not known to hold for _k and s.tail
  } else if (*) {
    assert Y#[_k](s);  // this is also the postcondition of the prefix lemma
  } else if (*) {
    assert Y#[_k+1](s);  // error: this is too much to ask for
  }
}

copredicate Z(s: Stream)
{
  false
}

colemma {:induction false} AlwaysLemma_Z(s: Stream)
  ensures Z(s);  // says, perversely, that Z(s) is always 'true'
{  // error: this had better not lead to a proof
  AlwaysLemma_Z(s);
}

function Doubles(n: int): Stream<int>
{
  Cons(2*n, Doubles(n + 1))
}

copredicate Even(s: Stream<int>)
{
  s.head % 2 == 0 && Even(s.tail)
}

colemma {:induction false} Lemma0(n: int)
  ensures Even(Doubles(n));
{
  Lemma0(n+1);
}

function UpwardBy2(n: int): Stream<int>
{
  Cons(n, UpwardBy2(n + 2))
}

colemma {:induction false} Lemma1(n: int)
  ensures Even(UpwardBy2(2*n));
{
  Lemma1(n+1);
}

colemma {:induction false} ProveEquality(n: int)
  ensures Doubles(n) == UpwardBy2(2*n);
{
  ProveEquality(n+1);
}

colemma {:induction false} BadEquality0(n: int)
  ensures Doubles(n) == UpwardBy2(n);
{  // error: postcondition does not hold
  BadEquality0(n+1);
}

colemma {:induction false} BadEquality1(n: int)
  ensures Doubles(n) == UpwardBy2(n+1);
{  // error: postcondition does not hold
  BadEquality1(n+1);
}

// test that match statements/expressions get the correct assumption (which wasn't always the case)

codatatype IList<T> = INil | ICons(head: T, tail: IList<T>)

ghost method M(s: IList)
{
  match (s) {
    case INil =>
      assert s == INil;
    case ICons(a, b) =>
      assert s == ICons(a, b);
  }
}

function G(s: IList): int
{
  match s
  case INil =>
    assert s == INil;
    0
  case ICons(a, b) =>
    assert s == ICons(a, b);
    0
}
