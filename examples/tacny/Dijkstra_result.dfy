// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
class _default {
  function f(n: nat): nat

  predicate P()
  {
    forall m: nat :: 
      f(f(m)) < f(m + 1)
  }

  lemma theorem()
    requires P()
    ensures forall n: nat :: f(n) == n
  {
    forall n: nat | true
      ensures f(n) == n
    {
      calc {
        f(n);
      ==
        {
          lemma_ping(n, n);
          lemma_pong(n);
        }
        n;
      }
    }
  }

  lemma lemma_ping(j: nat, n: nat)
    requires P()
    ensures j <= n ==> j <= f(n)
  {
    if 0 < j {
      calc {
        j <= f(n);
      ==
        j - 1 < f(n);
      <==
        j - 1 <= f(f(n - 1)) &&
        1 <= n;
      <==
        {
          lemma_ping(j - 1, f(n - 1));
        }
        j - 1 <= f(n - 1) &&
        1 <= n;
      <==
        {
          lemma_ping(j - 1, n - 1);
        }
        j - 1 <= n - 1 &&
        1 <= n;
      ==
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
    <==
      {
        lemma_monotonicity_0(n + 1, f(n));
      }
      f(f(n)) < f(n + 1);
    ==
      true;
    }
  }

  lemma lemma_monotonicity_0(a: nat, b: nat)
    requires P()
    ensures a <= b ==> f(a) <= f(b)
    decreases b - a
  {
    if a < b {
      calc {
        f(a);
      <=
        {
          lemma_monotonicity_1(a);
        }
        f(a + 1);
      <=
        {
          lemma_monotonicity_0(a + 1, b);
        }
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
    <=
      {
        lemma_ping(f(n), f(n));
      }
      f(f(n));
    <=
      f(n + 1);
    }
  }
}
