datatype List = Nil | Cons(e: Expr, ll: List)
datatype Expr = Const(c: int) | Var(v: int) | Nary(ni: int, nl: List)

function Subst(e: Expr, v: int, val: int): Expr
{
  match e
   case Const(c) => e
   case Var(x) => if x == v then Const(val) else e
   case Nary(op, args) => Nary(op, SubstList(args, v, val))
}

function SubstList(l: List, v: int, val: int): List
{
  match l
   case Nil => l
   case Cons(e, tail) => Cons(Subst(e, v, val), SubstList(tail, v, val))
}

lemma Const_Subst(n: int, v: int, val: int)
  ensures Subst(Const(n), v, val) == Const(n);
  {

  }

lemma Lemma(l: List, v: int, val: int)
    ensures SubstList(SubstList(l, v, val), v, val) == SubstList(l, v, val)
  {
    match l
	  case Nil =>
	  case Cons(e,ls) => Theorem(e,v,val);
  }
  
 lemma Theorem(l: Expr, v: int, val: int)
  ensures Subst(Subst(l, v, val), v, val) == Subst(l, v, val);
{
  match l
    case Const(v) =>
	case Var (v) => 
    case Nary(i,l) =>  Lemma(l,v,val);
}

 lemma LemmaHelp(l: List, v: int, val: int)
  ensures SubstList(SubstList(l, v, val), v, val) == SubstList(l, v, val)
{
 /*
  match l
   case Nil =>
   case Cons(x,xs) => Theorem(x,v,val);
 */
   tac(l);

}



tactic tac(b: Element)
{
  tmatch b {
    tvar vs := variables();
    tvar ls := lemmas();	
    explore(ls, vs);
	assert true;
	}
	assert true;
	assert true;
}
