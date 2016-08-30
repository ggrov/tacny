datatype natr = Zero | Succ(natr)

function add(x: natr, y: natr): natr
{
  match x
  case Zero => y
  case Succ(x') => Succ(add(x', y))
}

lemma prop_add_Zero(x: natr)
  ensures add(x, Zero) == x;
{ }

lemma prop_add_Succ(x: natr, y: natr)
  ensures Succ(add(x, y)) == add(x,Succ(y));
{ }

lemma prop_add_comm(x: natr, y: natr)
  ensures add(x, y) == add(y, x)
{
  match x {
    case Zero => 
      calc {
        add(Zero, y);
        ==
        y;
        == { prop_add_Zero(y); }
        add(y, Zero);
    }
    case Succ(x') =>
      calc {
        add(x,y);
        ==
        { assert x == Succ(x'); }
        add(Succ(x'), y);
        ==
        Succ(add(x', y));
        =={ prop_add_comm(x', y); }
        Succ(add(y, x'));
        == 
        add(y, Succ(x'));
      }
  }
}
