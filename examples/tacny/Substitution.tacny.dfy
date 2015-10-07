// RUN: %dafny /compile:0 /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype List = Nil | Cons(Expr, List)

datatype Expr =
  Const(int) |
  Var(int) |
  Nary(int, List)

function Subst(e: Expr, v: int, val: int): Expr
{
  match e
  case Const(c) => e
  case Var(x) => if x == v then Expr.Const(val) else e
  case Nary(op, args) => Expr.Nary(op, SubstList(args, v, val))
}

function SubstList(l: List, v: int, val: int): List
{
  match l
  case Nil => l
  case Cons(e, tail) => Cons(Subst(e, v, val), SubstList(tail, v, val))
}


lemma Theorem(l: Expr, v: int, val: int)
  ensures Subst(Subst(l, v, val), v, val) == Subst(l, v, val);
{
  mytac(l);
}

tactic mytac(l : Element)
{
  cases(l)
  {
    var l1 :| l1 in lemmas();
    var l2 :| l2 in lemmas();
    var v := merge(variables(), params());
    perm(l1, v);perm(l2,v);
  }
}

lemma Lemma(l: List, v: int, val: int)
  ensures SubstList(SubstList(l, v, val), v, val) == SubstList(l, v, val);
{
  mytac(l);
}