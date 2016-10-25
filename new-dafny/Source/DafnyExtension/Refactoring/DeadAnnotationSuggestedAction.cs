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
      var currentSnapshot = range.Snapshot;

      var first = _agg.GetTags(range).FirstOrDefault();
      if (first != null) {
        actionList.Add(new RemoveDeadAnnotationSuggestedAction(currentSnapshot, first.Tag));
      }

      Program p;
      RefactoringUtil.GetExistingProgram(currentSnapshot.TextBuffer, out p);
      if (p == null) return Enumerable.Empty<SuggestedActionSet>();
      var tld = RefactoringUtil.GetTld(p);
      MemberDecl md;
      RefactoringUtil.GetMemberFromPosition(tld, range.Start, out md);
      if (md != null) {
        var methodSpan = RefactoringUtil.GetRangeOfMember(currentSnapshot, md);
        var tagSpans = _agg.GetTags(methodSpan);
        var tags = (from ts in tagSpans
                    select new RemoveDeadAnnotationSuggestedAction(currentSnapshot, ts.Tag)).ToList();
        if(tags.Count > 0)
          actionList.Add(new RemoveMultipleDeadAnnotationsSuggestedAction(currentSnapshot.TextBuffer, tags, DeadAnnotationLocation.Block));
      }
      
      var snapshotWideSpan = new SnapshotSpan(currentSnapshot, 0, currentSnapshot.Length);
      var allTags = _agg.GetTags(snapshotWideSpan);
      var list = allTags.Select(ts => new RemoveDeadAnnotationSuggestedAction(currentSnapshot, ts.Tag)).ToList();
      if (list.Count > 0)
        actionList.Add(new RemoveMultipleDeadAnnotationsSuggestedAction(currentSnapshot.TextBuffer, list, DeadAnnotationLocation.File));
      
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
    protected DeadAnnotationSuggestedAction(ITextBuffer tb, DeadAnnotationLocation where) {
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
    public string DisplayText { get; }
    private readonly DeadAnnotationTag _tag;

    public RemoveDeadAnnotationSuggestedAction(ITextSnapshot snap, DeadAnnotationTag tag):base(snap.TextBuffer, DeadAnnotationLocation.Single) {
      DisplayText = tag.ActionContent;
      _replacement = tag.Replacement;
      _tag = tag;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = Tb.CreateEdit();
      Invoke(tedit);
      if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }
    
    internal void Invoke(ITextEdit tedit) {
      var snapSpan = _tag.TrackingReplacementSpan.GetSpan(Tb.CurrentSnapshot);
      tedit.Replace(snapSpan, _replacement);
    }
  }
  
  internal sealed class RemoveMultipleDeadAnnotationsSuggestedAction : DeadAnnotationSuggestedAction, ISuggestedAction
  {
    private readonly List<RemoveDeadAnnotationSuggestedAction> _dead;
    public string DisplayText => $"Remove/Simplify ALL dead annotations in {Loc} ({_dead.Count} found)";
    private string Loc => Where == DeadAnnotationLocation.File ? "file" : "block";

    public RemoveMultipleDeadAnnotationsSuggestedAction(ITextBuffer tb, List<RemoveDeadAnnotationSuggestedAction> deadAnnotations, DeadAnnotationLocation where): base(tb, where) {
      _dead = deadAnnotations;
    }

    public void Invoke(CancellationToken cancellationToken) {
      var tedit = Tb.CreateEdit();
      _dead.ForEach(x => x.Invoke(tedit));
      if(cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) { tedit.Dispose(); }
      tedit.Apply();
    }
  }
}