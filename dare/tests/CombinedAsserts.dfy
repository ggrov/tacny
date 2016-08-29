method Test() {
  
}

method FindZero_Assert(a: array<int>) returns (r: int)
  requires a != null && forall i :: 0 <= i < a.Length ==> 0 <= a[i];
  requires forall i :: 0 <= i-1 && i < a.Length ==> a[i-1]-1 <= a[i];
  ensures 0 <= r ==> r < a.Length && a[r] == 0;
  ensures r < 0 ==> forall i :: 0 <= i < a.Length ==> a[i] != 0;
{
  var n := 0;
  while (n < a.Length)
    invariant forall i :: 0 <= i < n && i < a.Length ==> a[i] != 0;
  {
    if (a[n] == 0) { return n; }
    assert (forall m {:induction} :: n <= m < n + a[n] && m < a.Length ==> n+a[n]-m <= a[m]) && 4 > 2 && 6 != 5;
    n := n + a[n];
  }
  return -1;
}