// RUN: %dafny /compile:0 /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module M0 {
  class Cell {
    var data: int;
    constructor (d: int)
      modifies this;
      ensures data == d;
    { data := d; }
  }
  class Counter {
    ghost var N: int;
    ghost var Repr: set<object>;
    protected predicate Valid()
      reads this, Repr;
    {
      this in Repr
    }

    constructor Init()
      modifies this;
      ensures N == 0;
      ensures Valid() && fresh(Repr - {this});
    {
      Repr := {};
      ghost var repr :| {this} <= repr && fresh(repr - {this});
      N, Repr := 0, repr;
    }

    method Inc()
      requires Valid();
      modifies Repr;
      ensures N == old(N) + 1;
      ensures Valid() && fresh(Repr - old(Repr));
    {
      N := N + 1;
      modify Repr - {this};
    }

    method Get() returns (n: int)
      requires Valid();
      ensures n == N;
    {
      n :| assume n == N;
    }
  }
}

module M1 refines M0 {
  class Counter {
    var c: Cell;
    var d: Cell;
    protected predicate Valid...
    {
      c != null && c in Repr &&
      d != null && d in Repr &&
      c != d &&
      N == c.data - d.data
    }

    constructor Init...
    {
      c := new Cell(0);
      d := new Cell(0);
      ...;
      ghost var repr := Repr + {this} + {c,d};
    }

    method Inc...
    {
      ...;
      modify ... {
        c.data := c.data + 1;
      }
    }

    method Get...
    {
      n := c.data - d.data;
    }
  }
}
