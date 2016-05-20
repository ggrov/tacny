// Dafny program verifier version 1.9.5.20511, Copyright (c) 2003-2015, Microsoft.
// Command Line Options: /rprint:- invar_tac_test.dfy
// main_program_id
datatype List = Nil | Cons(Expr, List)

datatype Expr = Const(int) | Var(int) | Nary(int, List)

class ddefault {
  function Subst(e: Expr, v: int, val: int): Expr
    decreases e, v, val
  {
    match e
    case Const(c: int) =>
      e
    case Var(x: int) =>
      if x == v then
        Expr.Const(val)
      else
        e
    case Nary(op: int, args: List) =>
      Expr.Nary(op, SubstList(args, v, val))
  }

  function SubstList(l: List, v: int, val: int): List
    decreases l, v, val
  {
    match l
    case Nil =>
      l
    case Cons(e: Expr, tail: List) =>
      Cons(Subst(e, v, val), SubstList(tail, v, val))
  }

  ghost method Theorem(l: Expr, v: int, val: int)
    ensures Subst(Subst(l, v, val), v, val) == Subst(l, v, val)
    decreases l, v, val
  {
    match l
    case Const(#2: int) =>
    case Var(#3: int) =>
    case Nary(#4: int, #5: List) =>
      Lemma(#5, v, val);
  }

  ghost method Lemma(l: List, v: int, val: int)
    ensures SubstList(SubstList(l, v, val), v, val) == SubstList(l, v, val)
    decreases l, v, val
  {
    match l
    case Nil =>
    case Cons(#0: Expr, #1: List) =>
      Theorem(#0, v, val);
  }
}