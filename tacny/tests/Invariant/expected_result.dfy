class _default {
  method Main()
  {
    var a: int := 0;
    var b: int := -1;
    var c: int := 0;
    var i: int := 100;
    while a != b
      invariant !(c < i) ==> !(a != b)
      decreases i - c
    {
      b := a;
      c := c + 1;
      if c < i {
        a := a + 1;
      }
    }
    print "Eureka";
  }
}
