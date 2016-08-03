// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" /autoTriggers:1 /printTooltips "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

predicate P(i: int)
predicate Q(i: int)

/* This file demonstrates a case where automatic trigger splitting is useful to
   prevent loop detection from reducing expressivity too much. */

lemma exists_0()
  requires P(0)
  ensures exists i {:split false} :: P(i) || (Q(i) ==> P(i+1)) {
    // Fails: P(i) is not a trigger
}

lemma forall_0(i: int)
  requires forall j {:split false} :: j >= 0 ==> (P(j) && (Q(j) ==> P(j+1)))
  requires i >= 0
  ensures P(i) {
    // Fails: P(i) is not a trigger
}


lemma exists_1()
  requires P(0)
  ensures exists i {:split false} :: P(i) || (Q(i) ==> P(i+1)) {
    assert Q(0) || !Q(0);
    // Works: the dummy assertion introduces a term that causes the quantifier
    // to trigger, producing a witness.
  }

lemma forall_1(i: int)
  requires forall j {:split false} :: j >= 0 ==> (P(j) && (Q(j) ==> P(j+1)))
  requires i >= 0
  ensures P(i) {
    assert Q(i) || !Q(i);
    // Works: the dummy assertion introduces a term that causes the quantifier
    // to trigger, producing a witness.
}


lemma exists_2()
  requires P(0)
  ensures exists i :: P(i) || (Q(i) ==> P(i+1)) {
    // Works: automatic trigger splitting allows P(i) to get its own triggers
}

lemma forall_2(i: int)
  requires forall j :: j >= 0 ==> (P(j) && (Q(j) ==> P(j+1)))
  requires i >= 0
  ensures P(i) {
    // Works: automatic trigger splitting allows P(i) to get its own triggers
}


lemma loop()
  requires P(0)
  requires forall i {:matchingloop} :: i >= 0 ==> Q(i) && (P(i) ==> P(i+1))
  ensures P(100) {
    // Works: the matching loop is explicitly allowed
}
