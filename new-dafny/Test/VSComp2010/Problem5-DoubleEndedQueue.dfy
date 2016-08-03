// RUN: %dafny /compile:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// VSComp 2010, problem 5, double-ended queue.
// Rustan Leino, 18 August 2010.
//
// This program employs the standard Valid()/Repr idiom used in the dynamic-frames
// style of specifications, see for example the comment in Problem3-FindZero.dfy.
// Within that idiom, the specification is straightforward (if verbose), and there
// are no particular wrinkles or annoyances in getting the verifier to prove the
// correctness.

class AmortizedQueue<T> {
  // The front of the queue.
  var front: LinkedList<T>;
  // The rear of the queue (stored in reversed order).
  var rear: LinkedList<T>;

  ghost var Repr: set<object>;
  ghost var List: seq<T>;

  function Valid(): bool
    reads this, Repr;
  {
    this in Repr &&
    front != null && front in Repr && front.Repr <= Repr && front.Valid() &&
    rear != null && rear in Repr && rear.Repr <= Repr && rear.Valid() &&
    |rear.List| <= |front.List| &&
    List == front.List + rear.ReverseSeq(rear.List)
  }

  method Init()
    modifies this;
    ensures Valid() && List == [];
  {
    front := new LinkedList<T>.Init();
    rear := new LinkedList<T>.Init();
    Repr := {this};
    Repr := Repr + front.Repr + rear.Repr;
    List := [];
  }

  method InitFromPieces(f: LinkedList<T>, r: LinkedList<T>)
    requires f != null && f.Valid() && r != null && r.Valid();
    modifies this;
    ensures Valid() && List == f.List + r.ReverseSeq(r.List);
  {
    if (r.length <= f.length) {
      front := f;
      rear := r;
    } else {
      var rr := r.Reverse();
      var ff := f.Concat(rr);
      front := ff;

      rear := new LinkedList<T>.Init();
    }
    Repr := {this};
    Repr := Repr + front.Repr + rear.Repr;
    List := front.List + rear.ReverseSeq(rear.List);
  }

  method Front() returns (t: T)
    requires Valid() && List != [];
    ensures t == List[0];
  {
    t := front.head;
  }

  method Tail() returns (r: AmortizedQueue<T>)
    requires Valid() && List != [];
    ensures r != null && r.Valid() && r.List == List[1..];
  {
    r := new AmortizedQueue<T>.InitFromPieces(front.tail, rear);
  }

  method Enqueue(item: T) returns (r: AmortizedQueue<T>)
    requires Valid();
    ensures r != null && r.Valid() && r.List == List + [item];
  {
    var rr := rear.Cons(item);
    r := new AmortizedQueue<T>.InitFromPieces(front, rr);
  }
}


class LinkedList<T> {
  var head: T;
  var tail: LinkedList<T>;
  var length: int;

  ghost var List: seq<T>;
  ghost var Repr: set<LinkedList<T>>;

  function Valid(): bool
    reads this, Repr;
  {
    this in Repr &&
    0 <= length && length == |List| &&
    (length == 0 ==> List == [] && tail == null) &&
    (length != 0 ==>
      tail != null && tail in Repr &&
      tail.Repr <= Repr && this !in tail.Repr &&
      tail.Valid() &&
      List == [head] + tail.List &&
      length == tail.length + 1)
  }

  method Init()
    modifies this;
    ensures Valid() && List == [];
  {
    tail := null;
    length := 0;
    List := [];
    Repr := {this};
  }

  method Cons(d: T) returns (r: LinkedList<T>)
    requires Valid();
    ensures r != null && r.Valid() && r.List == [d] + List;
  {
    r := new LinkedList<T>;
    r.head := d;
    r.tail := this;
    r.length := length + 1;
    r.List := [d] + List;
    r.Repr := {r} + Repr;
  }

  method Concat(end: LinkedList<T>) returns (r: LinkedList<T>)
    requires Valid() && end != null && end.Valid();
    ensures r != null && r.Valid() && r.List == List + end.List;
    decreases Repr;
  {
    if (length == 0) {
      r := end;
    } else {
      var c := tail.Concat(end);
      r := c.Cons(head);
    }
  }

  method Reverse() returns (r: LinkedList<T>)
    requires Valid();
    ensures r != null && r.Valid() && |List| == |r.List|;
    ensures (forall k :: 0 <= k && k < |List| ==> List[k] == r.List[|List|-1-k]);
    ensures r.List == ReverseSeq(List);
    decreases Repr;
  {
    if (length == 0) {
      r := this;
    } else {
      r := tail.Reverse();
      var e := new LinkedList<T>.Init();
      e := e.Cons(head);
      r := r.Concat(e);
    }
  }

  static function ReverseSeq(s: seq<T>): seq<T>
  {
    if s == [] then [] else
    ReverseSeq(s[1..]) + [s[0]]
  }
}
