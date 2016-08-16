﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text.Tagging;

namespace DafnyLanguage.Refactoring
{
  #region mefprovider
  [Export(typeof(ISuggestedActionsSourceProvider))]
  [Name("DeadAnnotationSuggestedActions")]
  [ContentType("dafny")]
  internal class DeadAnnotationSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService AggFactory { get; set; }

    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
      var aggregator = AggFactory.CreateTagAggregator<DeadAnnotationTag>(textBuffer);
      Func<DeadAnnotationSuggestedActionsSource> propertyObject = () => new DeadAnnotationSuggestedActionsSource(aggregator);
      return textBuffer.Properties.GetOrCreateSingletonProperty(typeof(DeadAnnotationSuggestedActionsSource), propertyObject);
    }
  }
  #endregion

  internal class DeadAnnotationSuggestedActionsSource : ISuggestedActionsSource
  {
    private readonly ITagAggregator<DeadAnnotationTag> _agg;
    
    public DeadAnnotationSuggestedActionsSource(ITagAggregator<DeadAnnotationTag> aggregator) {
      _agg = aggregator;
    }

    public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
      return Task.Factory.StartNew(() => _agg.GetTags(range).Any(), cancellationToken);
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
      RemoveDeadAnnotationSuggestedAction actionOne = null;
      RemoveAllDeadAnnotationsSuggestedAction actionAll = null;

      var first = _agg.GetTags(range).FirstOrDefault();
      if (first != null) {
        var snapSpan = first.Tag.Snapshot.CreateTrackingSpan(first.Tag.ReplacementSpan, SpanTrackingMode.EdgeInclusive);
        actionOne = new RemoveDeadAnnotationSuggestedAction(snapSpan, first.Tag.Snapshot, first.Tag.Replacement);
      }
      
      var snapshotWideSpan = new SnapshotSpan(range.Snapshot, 0, range.Snapshot.Length);
      var allTags = _agg.GetTags(snapshotWideSpan);
      var list = allTags.Select(ts => ts.Tag).ToList();
      if (list.Count > 0)
        actionAll = new RemoveAllDeadAnnotationsSuggestedAction(range.Snapshot.TextBuffer, list);

      if (actionOne != null) {
        return new [] { new SuggestedActionSet(new ISuggestedAction[] { actionOne, actionAll }) };
      }

      return actionAll != null 
        ? new[] { new SuggestedActionSet(new ISuggestedAction[] { actionAll }) } 
        : Enumerable.Empty<SuggestedActionSet>();
    }

    #region unused implementation
    public event EventHandler<EventArgs> SuggestedActionsChanged { add{} remove{} }
    public void Dispose() {}
    public bool TryGetTelemetryId(out Guid telemetryId){telemetryId = Guid.Empty;return false;}
    #endregion
  }

  internal class RemoveDeadAnnotationSuggestedAction : ISuggestedAction
  {
    private readonly SnapshotSpan _snapSpan;
    private readonly ITextBuffer _tb;
    private readonly string _replacement;
    public string DisplayText => "Remove this dead code";

    public RemoveDeadAnnotationSuggestedAction(ITrackingSpan span, ITextSnapshot snap, string replacement) {
      _tb = snap.TextBuffer;
      _snapSpan = span.GetSpan(snap);
      _replacement = replacement;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = _tb.CreateEdit();
      tedit.Replace(_snapSpan, _replacement);
      if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }

    #region constant properties
    public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>("");
    public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    public bool HasActionSets => false;
    public ImageMoniker IconMoniker => default(ImageMoniker);
    public string IconAutomationText => null;
    public string InputGestureText => null;
    public bool HasPreview => false;
    public void Dispose(){}
    public bool TryGetTelemetryId(out Guid telemetryId){telemetryId = Guid.Empty;return false;}
    #endregion
  }


  internal class RemoveAllDeadAnnotationsSuggestedAction : ISuggestedAction
  {
    private readonly ITextBuffer _tb;
    private readonly List<DeadAnnotationTag> _dead;
    public string DisplayText => $"Remove ALL dead code in file ({_dead.Count} found)";

    public RemoveAllDeadAnnotationsSuggestedAction(ITextBuffer tb, List<DeadAnnotationTag> deadAnnotations) {
      _tb = tb;
      _dead = deadAnnotations;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = _tb.CreateEdit();
      _dead.ForEach(x => tedit.Replace(x.ReplacementSpan.Span, x.Replacement));
      if(cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }

    #region constant properties
    public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>("");
    public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    public bool HasActionSets => false;
    public ImageMoniker IconMoniker => default(ImageMoniker);
    public string IconAutomationText => null;
    public string InputGestureText => null;
    public bool HasPreview => false;
    public void Dispose() { }
    public bool TryGetTelemetryId(out Guid telemetryId) { telemetryId = Guid.Empty; return false; }
    #endregion
  }
}