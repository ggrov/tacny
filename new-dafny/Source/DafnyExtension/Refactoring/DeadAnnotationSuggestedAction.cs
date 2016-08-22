using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text.Tagging;

namespace DafnyLanguage.Refactoring
{

  internal enum DeadAnnotationLocation { Single = 0, Block = 1, File = 2 }

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
      var actionList = new List<ISuggestedAction>();

      var first = _agg.GetTags(range).FirstOrDefault();
      if (first != null) {
        actionList.Add(new RemoveDeadAnnotationSuggestedAction(first.Tag.TrackingReplacementSpan, first.Tag.Snapshot, first.Tag.Replacement, first.Tag.TypeName));
      }

      Program p;
      RefactoringUtil.GetExistingProgram(range.Snapshot.TextBuffer, out p);
      if (p == null) return Enumerable.Empty<SuggestedActionSet>();
      var tld = RefactoringUtil.GetTld(p);
      MemberDecl md;
      RefactoringUtil.GetMemberFromPosition(tld, range.Start, out md);
      if (md != null) {
        var methodSpan = RefactoringUtil.GetRangeOfMember(range.Snapshot, md);
        var tagSpans = _agg.GetTags(methodSpan);
        var tags = (from ts in tagSpans select ts.Tag).ToList();
        actionList.Add(new RemoveMultipleDeadAnnotationsSuggestedAction(range.Snapshot.TextBuffer, tags, DeadAnnotationLocation.Block));
      }
      
      var snapshotWideSpan = new SnapshotSpan(range.Snapshot, 0, range.Snapshot.Length);
      var allTags = _agg.GetTags(snapshotWideSpan);
      var list = allTags.Select(ts => ts.Tag).ToList();
      if (list.Count > 0)
        actionList.Add(new RemoveMultipleDeadAnnotationsSuggestedAction(range.Snapshot.TextBuffer, list, DeadAnnotationLocation.File));
      
      return actionList.Count>0 ?
        new [] { new SuggestedActionSet(actionList.ToArray()) }
        : Enumerable.Empty<SuggestedActionSet>();
    }

    #region unused implementation
    public event EventHandler<EventArgs> SuggestedActionsChanged { add{} remove{} }
    public void Dispose() {}
    public bool TryGetTelemetryId(out Guid telemetryId){telemetryId = Guid.Empty;return false;}
    #endregion
  }
  
  internal class DeadAnnotationSuggestedAction
  {
    public readonly DeadAnnotationLocation Where;
    protected readonly ITextBuffer Tb;
    public DeadAnnotationSuggestedAction(ITextBuffer tb, DeadAnnotationLocation where) {
      Tb = tb;
      Where = where;
    }
    public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>("");
    public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    public bool HasActionSets => false;
    public ImageMoniker IconMoniker => default(ImageMoniker);
    public string IconAutomationText => null;
    public string InputGestureText => null;
    public bool HasPreview => false;
    public void Dispose() { }
    public bool TryGetTelemetryId(out Guid telemetryId) { telemetryId = Guid.Empty; return false; }
  }

  internal sealed class RemoveDeadAnnotationSuggestedAction : DeadAnnotationSuggestedAction, ISuggestedAction
  {
    private readonly string _replacement;
    public string DisplayText => $"{_action} this {_adjective} {_typename}";
    private readonly string _action;
    private readonly string _adjective;
    private readonly string _typename;
    private readonly SnapshotSpan _snapSpan;

    public RemoveDeadAnnotationSuggestedAction(ITrackingSpan span, ITextSnapshot snap, string replacement, string typename):base(snap.TextBuffer, DeadAnnotationLocation.Single) {
      _action = string.IsNullOrEmpty(replacement) ? "Remove" : "Simplify";
      _adjective = string.IsNullOrEmpty(replacement) ? "dead" : "overspecific";
      _snapSpan = span.GetSpan(snap);
      _replacement = replacement;
      _typename = typename;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = Tb.CreateEdit();
      tedit.Replace(_snapSpan, _replacement);
      if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }
  }
  
  internal sealed class RemoveMultipleDeadAnnotationsSuggestedAction : DeadAnnotationSuggestedAction, ISuggestedAction
  {
    private readonly List<DeadAnnotationTag> _dead;
    public string DisplayText => $"Remove/Simplify ALL dead annotations in {Loc} ({_dead.Count} found)";
    private string Loc => Where == DeadAnnotationLocation.File ? "file" : "block";

    public RemoveMultipleDeadAnnotationsSuggestedAction(ITextBuffer tb, List<DeadAnnotationTag> deadAnnotations, DeadAnnotationLocation where): base(tb, where) {
      _dead = deadAnnotations;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = Tb.CreateEdit();
      _dead.ForEach(x => tedit.Replace(x.ReplacementSpan.Span, x.Replacement));
      if(cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }
  }
}