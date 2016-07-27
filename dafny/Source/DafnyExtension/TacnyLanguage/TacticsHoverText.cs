using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage.TacnyLanguage
{
  internal static class TacticsHoverText
  {
    public static void TestForAndAddHoverText(ref ITrackingSpan applicableToSpan, SnapshotPoint trigger, ITextBuffer tb, IList<object> quickInfoContent)
    {
      var methodName = new SnapshotSpan();
      var expanded = TacticReplacerProxy.GetExpandedForPreview(trigger.Position, tb, ref methodName);
      if (string.IsNullOrEmpty(expanded)) return;
      applicableToSpan = tb.CurrentSnapshot.CreateTrackingSpan(methodName, SpanTrackingMode.EdgeExclusive);
      quickInfoContent.Add(expanded);
    }
  }
}