class _default {
  lemma P54()
    ensures forall m, n :: minus(add(m, n), n) == m
  {
    assert forall m, n :: minus(n, add(n, m)) == True;
    assert forall m, n :: add(m, n) == add(n, m);
  }

  lemma P67()
    ensures forall m, n :: leq(n, add(m, n)) == True
  {
    assert forall m, n :: leq(n, add(n, m)) == True;
    assert forall m, n :: add(m, n) == add(n, m);
  }
}
