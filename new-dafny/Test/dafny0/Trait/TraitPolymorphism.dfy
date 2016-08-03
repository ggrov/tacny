// RUN: %dafny /compile:0 /print:"%t.print" /dprint:"%t.dprint" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait T1
{
   var f: int;
   
   function method Plus (x:int, y:int) : int
     requires x>y;
   {
      x + y
   }
   
   function method Mul (x:int, y:int, z:int) : int
     requires x>y;
   {
     x * y * z
   }
   
   //function method BodyLess1() : int
   
   static method GetPhoneNumber (code:int, n:int) returns (z:int)
   {
     z := code + n;
   }
   
   method TestPhone ()
   {
     var num : int;
     num := GetPhoneNumber (10, 30028);
   }
}

trait T2
{
}

class C1 extends T1
{
    method P2(x:int, y:int) returns (z:int)
      requires x>y;
    {
       z:= Plus(x,y) + Mul (x,y,1);
    }
}



method Good(c: C1) returns (t: T1)
ensures c == t;
{
    t := c;    
}

method Bad1(c: C1) returns (t: T2)
ensures c == t;
{
    t := c;  //error, C1 has not implemented T2
}

method Bad2(c: C1) returns (t: T1)
ensures c == t;
{
    c := t;  //error, can not assign a trait to a class
}
