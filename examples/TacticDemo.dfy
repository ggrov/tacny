
datatype List = Nil | Cons(le: Expr, ll : List)
datatype Expr = Const(ec : int) | Var(ev:int) | Nary(en:int, eargs:List)


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

tactic mytac(l : Element)
{
    tvar lem := lemmas(); 
    tmatch l {
	   tvar vars := variables();
	   explore(lem,vars);
	}
  }

 lemma Theorem(l: Expr, v: int, val: int)
  ensures Subst(Subst(l, v, val), v, val) == Subst(l, v, val);
{
  mytac(l); 
}


















