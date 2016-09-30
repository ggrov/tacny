 // trivial lemma but required for the next proof
  lemma set_eq_simple(A : set<int>, B:set<int>, x :int)
    requires x in A;
	requires A == B;
	ensures x in B;
	{
	
	}

  lemma set_inter_empty_contr(A : set<int>, B:set<int>, x :int)
    requires x in A;
	requires A * B == {};
	ensures x !in B
  {
     if x in B {
		assert  x in A * B;
		set_eq_simple(A*B,{},x);
		assert x in {};
		assert false;
	  }
  }



