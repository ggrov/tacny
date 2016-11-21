 lemma lem(A : set<int>, B:set<int>, x :int)
    requires x in A;
	requires A == B;
	ensures x in B;
	{	}

  lemma ex2(A : set<int>, B:set<int>, x :int)
    requires x in A;
	requires A * B == {};
	ensures x !in B
  {
      if x in B {
	    assert x in A*B;
		lem(A*B,{},x);
		assert x in {};
	    assert false;
	  }
  }


  lemma set_inter_empty_contr(A : set<int>, B:set<int>, x :int)
    requires x in A;
	requires A * B == {};
	ensures x !in B
  {
     if x in B {
		assert  x in A * B;
		lem(A*B,{},x);
		assert x in {};
		assert false;
	  }
  }


















