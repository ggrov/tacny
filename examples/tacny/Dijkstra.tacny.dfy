function f(n: nat) : nat

predicate P()
{
  forall m: nat :: f(f(m)) < f(m + 1)
}

lemma theorem()
  requires P()
  ensures forall n: nat :: f(n) == n
{
  forall n: nat
    ensures f(n) == n
  {
    calc {
      f(n);
    ==  { lemma_ping(n, n); lemma_pong(n); }
      n;
    }
  }
}

lemma lemma_ping(j: nat, n: nat)
  requires P()
  ensures j <= n ==> j <= f(n)
{
  if 0 < j 
  {
    calc {
      j <= f(n);
    ==
      j - 1 < f(n);
    <== // P with m := n - 1
      j - 1 <= f(f(n - 1)) && 1 <= n;
    <== { lemma_ping(j - 1, f(n - 1)); }
      j - 1 <= f(n - 1) && 1 <= n;
    <== { lemma_ping(j - 1, n - 1); }
      j - 1 <= n - 1 && 1 <= n;
    == // 0 < j
      j <= n;
    }
  }
}

lemma lemma_pong(n: nat)
  requires P()
  ensures f(n) <= n
{
  calc {
    f(n) <= n;
  ==
    f(n) < n + 1;
  <==  { lemma_monotonicity_0(n + 1, f(n)); }
    f(f(n)) < f(n + 1);
  ==  // P with m := n
    true;
  }
}

lemma lemma_monotonicity_0(a: nat, b: nat)
  requires P()
  ensures a <= b ==> f(a) <= f(b)  // or, equivalently:  f(b) < f(a) ==> b < a
{
  variant();
  if a < b {
    calc {
      f(a);
    <=  { lemma_monotonicity_1(a); }
      f(a + 1);
    <=  { lemma_monotonicity_0(a + 1, b); }
      f(b);
    }
  }
}

lemma lemma_monotonicity_1(n: nat)
  requires P()
  ensures f(n) <= f(n + 1)
{
  calc {
    f(n);
  <=  { lemma_ping(f(n), f(n)); }
    f(f(n));
  <=  // (0)
    f(n + 1);
  }
}

tactic variant(){

  solved {

    var f :| f in params();

    add_variant(f) || {var y :|  y in params() && f != y; 
    add_variant(f-y);};
  }
}