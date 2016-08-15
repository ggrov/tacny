using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage.Refactoring
{
  internal static class TacticsHoverText
  {
    public static void TestForAndAddHoverText(ref ITrackingSpan applicableToSpan, SnapshotPoint trigger, ITextBuffer tb, IList<object> quickInfoContent)
    {
      Contract.Requires(trigger != null);
      Contract.Requires(tb != null);
      var methodName = new SnapshotSpan();
      var expanded = TacticReplacerProxy.GetExpandedForPreview(trigger.Position, tb, ref methodName);
      if (string.IsNullOrEmpty(expanded)) return;
      applicableToSpan = tb.CurrentSnapshot.CreateTrackingSpan(methodName, SpanTrackingMode.EdgeExclusive);
      quickInfoContent.Add(expanded);
    }
  }
}