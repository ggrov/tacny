predicate method is_even(s : int)
{
	s % 2 == 0
}

tactic invar_tactic(i : int, f : bool) 
{	
	var aaa := extract_guard();
    aaa := replace_operator("<", "<=", aaa);
   	var invar2 := create_invariant(aaa);
    add_invariant(invar2);
    is_valid();
	/*var qqq := replace_singleton(i, |s|, f);  // replace |s| with i in formula f
	var invar := create_invariant(qqq); // creates an invariant from a given expression
    add_invariant(invar);*/

}

method max(s : seq<char>) returns (max : char)
ensures forall k :: 0 <= k < |s| ==> max >= s[k]
{
	var i := 0;
	while (i < |s|)
	// go through every updatestmt if its in tac dic resolve tac body
	invar_tactic(i, forall k :: 0 <= k < |s| ==> max >= s[k]);
	{
		if(s[i] >= max) {
			max := s[i];
		} 
		i := i + 1;
	}
}

/*
method min_max(s : seq<char>) returns (min : char, max : char)
ensures max > min ==> min < max
ensures forall k :: 0 <= k < |s| ==> s[k] <= max 
ensures forall k :: 0 <= k < |s| ==> s[k] >= min
{
	
	var i := 0;
	while i < |s|
	invar_tactic(i);
	{
		if(s[i] >= max)
		{
			max := s[i];
		} 
		if(s[i] <= min)
		{
			min := s[i];
		}
		i := i + 1;
	}
}


method max_odd(s : seq<int>) returns (max : int, data : seq<int>)
ensures forall k :: 0 <= k < |data| ==> is_even(data[k])
ensures forall k :: 0 <= k < |s| ==> s[k] <= max
{
	var i := 0;
	data := [];
	max := 0;
	while i < |s|
	invar_tactic(i);
	{	
		if(s[i] >= max)
		{
			max := s[i];
		}
		if(is_even(s[i])) {
			data := data + [s[i]];
		}

		i := i + 1;

	}

}


method remove_odd(s : seq<int>) returns (data : seq<int>)
ensures forall k :: 0 <= k < |data| ==> data[k] % 2 == 0
{
	var i := 0;
	data := []; 
	while i < |s|
	invar_tactic(i);
	//invariant forall k :: 0 <= k < |data| ==> data[k] % 2 == 0 // same as post-condition
	{
		if(is_even(s[i])) {
			data := data + [s[i]];
		}
		i := i + 1;
	}
}
*/

/*
method find(index : int, s : seq<int>) returns (res : int)
requires 0 !in s
ensures res == -1 ==> forall k :: 0 <= k < |s| ==> s[k] != index // base case
ensures 0 <= res < |s| ==> s[res] == index
{
	var i := 0;
	res := -1;
	while i < |s|
	invariant i <= |s|
	invariant res == -1 ==> forall k :: 0 <= k < i ==> s[k] != index;
	//invar_tactic(i);
	{
		if(s[i] == index) {
			res := i;
			break;
		}

		i := i + 1;
	}
}

method {:test(1,2,3)} all_even(s : seq<int>) returns (data : seq<int>)
ensures |data| <= 0 ==> forall k :: 0 <= k < |s| ==> is_even(s[k]) == false // base case
ensures |data| > 0 ==> forall k :: 0 <= k < |data| ==> is_even(data[k])
{
	var i := 0;
	data := [];
	while i < |s|
	invariant i <= |s|
	invariant |data| > 0 ==> forall k :: 0 <= k < |data| ==> is_even(data[k])
	invariant |data| == 0 ==> forall k :: 0 <= k < i ==> is_even(s[k]) == false
	//invar_tactic(i);
	{
		if (is_even(s[i])) {
			data := data + [s[i]];
		}

		i := i + 1;
	}
}

*/
method Main()
{
	//var tmp := max(['a', 'b', 'c', 'd']);
	//var tmp := remove_odd([1,2,3,4]);
}