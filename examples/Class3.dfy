tactic dummyT1 (){ 
  tvar i := 0;
  //assume false;
  assume true;
  //assert false;
  assert true;
}

lemma dummy ()
//ensures false
{
	var x := 0;
	var y := 1;
	
	dummyT1();
}
