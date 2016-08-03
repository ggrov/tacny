//RUN: %dafny /dafnyVerify:0 /compile:0 /env:0 /rprint:"%t.dfy" "%s"
//RUN: %dafny /dafnyVerify:0 /compile:0 /env:0 /printMode:DllEmbed /rprint:"%t1.dfy" "%t.dfy"
//RUN: %dafny /env:0 /out:"%s.dll" /printMode:DllEmbed /rprint:"%t2.dfy" "%t1.dfy" > "%t.output"
//RUN: %diff "%t1.dfy" "%t2.dfy"
//RUN: %diff "%t.output" "%s.expect"

abstract module S {
  class C {
    var f: int;
    ghost var g: int;
    var h: int;
    method m() modifies this
  }
}

module T refines S {
  class C {
    ghost var h: int;
    ghost var j: int;
    var k: int;
    method m() 
    ensures h == h
    ensures j == j
     {
      assert k == k;
    }
  }
}
 
