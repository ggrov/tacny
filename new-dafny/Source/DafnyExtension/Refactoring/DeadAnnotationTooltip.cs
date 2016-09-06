using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage.Refactoring
{
  internal static class DeadAnnotationTooltip
  {
    public static void TestForAndAddHoverText(ref ITrackingSpan applicableToSpan, ITextBuffer tb, SnapshotPoint triggerPoint, IList<object> quickInfoContent) {
      var tagger = tb.Properties.GetProperty(typeof(DeadAnnotationTagger)) as DeadAnnotationTagger;
      var tag = tagger?.GetTags(new NormalizedSnapshotSpanCollection(new List<SnapshotSpan> {new SnapshotSpan(triggerPoint, triggerPoint)})).FirstOrDefault();
      if (tag == null) return;
      applicableToSpan = tag.Tag.TrackingReplacementSpan;
      quickInfoContent.Clear();
      quickInfoContent.Add(tag.Tag.ToolTipContent);
    }
  }
}