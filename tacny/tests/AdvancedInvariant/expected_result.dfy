class _default {
  method FindMax(a: array<int>) returns (i: int)
    requires a != null
    requires a.Length > 0
    ensures 0 <= i && i < a.Length && forall k :: 0 <= k && k < a.Length ==> a[i] >= a[k]
  {
    var idx := 0;
    var j := idx;
    i := idx;
    while idx < a.Length
      invariant idx <= a.Length
      invariant 0 <= i && i < a.Length
      invariant forall k :: j <= k && k < idx ==> a[i] >= a[k]
    {
      if a[idx] > a[i] {
        i := idx;
      }
      idx := idx + 1;
    }
  }
}
