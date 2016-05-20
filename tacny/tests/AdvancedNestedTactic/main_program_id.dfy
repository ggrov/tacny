class _default {
  predicate even(n: nat)
  {
    if n == 0 then
      true
    else if n == 1 then
      false
    else
      !odd(n - 1)
  }

  predicate odd(n: nat)
  {
    if n == 1 then
      true
    else if n == 0 then
      false
    else
      !even(n - 1)
  }

  lemma even_or_odd(n: nat)
    ensures even(n) || odd(n)
  {
    if n == 0 {
    } else {
      even_or_odd(n - 1);
    }
  }
}
