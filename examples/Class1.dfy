method m()
ensures false
{
t();
  assume false;
}

tactic t(){
  assert false;
}