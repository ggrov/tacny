
datatype Dummy = A | B
datatype Dummy2 = A2 | B2

lemma dummyLemma(m: Dummy)
ensures false
{
	assume false;
}

lemma ltest(d : Dummy)
 ensures false
{
   tac(d);
  //dummyLemma(d);

}

tactic tac(b: Element)
{

    tvar vs := variables();
    tvar ls := lemmas();	
    explore(ls, vs);
}
