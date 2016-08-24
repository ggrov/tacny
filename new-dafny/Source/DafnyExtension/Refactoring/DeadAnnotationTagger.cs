﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Program = Microsoft.Dafny.Program;
using Dary;
using Tacny;

namespace DafnyLanguage.Refactoring
{
  #region mefprovider
  [Export(typeof(ITaggerProvider))]
  [ContentType("dafny")]
  [Name("DeadAnnotationTaggerProvider")]
  [TagType(typeof(DeadAnnotationTag))]
  internal class DeadAnnotationTaggerProvider : ITaggerProvider
  {
    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider Isp { get; set; }
    
    [Import]
    internal IClassificationTypeRegistryService Ctr { get; set; }

    [Import]
    internal ITextDocumentFactoryService Tdf { get; set; }

    [Import]
    internal ITextStructureNavigatorSelectorService Tsn { get; set; }

    [Import]
    internal IBufferTagAggregatorFactoryService AggregatorFactory { get; set; }

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
      Func<ITagger<T>> taggerProperty = delegate {
        var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
        if (vsShell == null) throw new NullReferenceException("VS Shell failed to Load");
        IVsPackage shellPack;
        var packToLoad = new Guid("e1baf989-88a6-4acf-8d97-e0dc243476aa");
        if (vsShell.LoadPackage(ref packToLoad, out shellPack) != VSConstants.S_OK)
          throw new NullReferenceException("Dafny Menu failed to Load");
        var dafnyMenuPack = (DafnyMenuPackage)shellPack;
        dafnyMenuPack.DeadCodeMenuProxy = new DeadCodeMenuProxy();

        DeadAnnotationTag.Type = Ctr.GetClassificationType("Dead Annotation");
        RefactoringUtil.Tdf = RefactoringUtil.Tdf ?? Tdf;
        var status = (IVsStatusbar)Isp.GetService(typeof(IVsStatusbar));
        var tsn = Tsn.GetTextStructureNavigator(buffer);
        var agg = AggregatorFactory.CreateTagAggregator<ProgressGlyphTag>(buffer);

        return new DeadAnnotationTagger(buffer, status, tsn, agg) as ITagger<T>;
      };
      return buffer.Properties.GetOrCreateSingletonProperty(typeof(DeadAnnotationTagger), taggerProperty);
    }
  }
  
  [Export(typeof(EditorFormatDefinition))]
  [ClassificationType(ClassificationTypeNames = "Dead Annotation")]
  [Name("Dead Annotation")]
  [UserVisible(true)]
  [Order(Before = Priority.Default)]
  internal sealed class DeadAnnotationClassificationFormat : ClassificationFormatDefinition
  {
    public DeadAnnotationClassificationFormat() {
      DisplayName = "Dead Annotation";
      ForegroundOpacity = 0.6;
    }
  }

  internal static class DeadAnnotationClassificationAttribute
  {
    [Export(typeof(ClassificationTypeDefinition))]
    [Name("Dead Annotation")]
    internal static ClassificationTypeDefinition UserType { get; set; }
  }
  #endregion

  #region tags
  internal sealed class DeadAnnotationTag : ClassificationTag, IErrorTag
  {
    public static IClassificationType Type;
    public string ErrorType => PredefinedErrorTypeNames.OtherError;
    public object ToolTipContent => $"Dead {TypeName} can safely be removed";

    public readonly string Replacement;
    public readonly SnapshotSpan WarnSpan;
    public readonly ITrackingSpan TrackingReplacementSpan;
    public readonly ITextSnapshot OriginalSnapshot;
    public readonly string TypeName;
    public readonly Program Program;
    
    public DeadAnnotationTag(ITextSnapshot originalSnapshot, int warnStart, int warnLength,
      int replaceStart, int replaceLength, string replacement, string typeName, Program program) : base(Type) {
      OriginalSnapshot = originalSnapshot;
      Replacement = replacement;
      WarnSpan = new SnapshotSpan(originalSnapshot, warnStart, warnLength);
      var replacementSpan = new SnapshotSpan(originalSnapshot, replaceStart, replaceLength);
      TrackingReplacementSpan = originalSnapshot.CreateTrackingSpan(replacementSpan, SpanTrackingMode.EdgeExclusive, TrackingFidelityMode.Forward);
      TypeName = typeName;
      Program = program;
    }
  }
  #endregion

  internal class DeadCodeMenuProxy : IDeadCodeMenuProxy {
    public ISuggestedAction GetSuggestedAction(ITextView tv, int dali) {
      var dal = (DeadAnnotationLocation) dali;
      DeadAnnotationSuggestedActionsSource dasap;
      if (!tv.TextBuffer.Properties.TryGetProperty(typeof(DeadAnnotationSuggestedActionsSource), out dasap)) return null;
      var position = new SnapshotSpan(tv.Caret.Position.BufferPosition, 1);
      var acts = dasap.GetSuggestedActions(null, position, CancellationToken.None);
      var action = (from actSet in acts
        from act in actSet.Actions
        select act as DeadAnnotationSuggestedAction
        into dasa
        where dasa.Where == dal
        select dasa).FirstOrDefault();
      if (dal == DeadAnnotationLocation.Single) {
        var rdasa = action as RemoveDeadAnnotationSuggestedAction;
        return rdasa;
      }
      var rmdasa = action as RemoveMultipleDeadAnnotationsSuggestedAction;
      return rmdasa;
    }

    public bool Toggle() {
      DeadAnnotationTagger.Checkers.ForEach(x => x.Stop=true);
      DeadAnnotationTagger.Enabled = !DeadAnnotationTagger.Enabled;
      return DeadAnnotationTagger.Enabled;
    }
  }

  internal class DeadAnnotationTagger : ITagger<DeadAnnotationTag>, IDisposable
  {
    #region fields and properties
    internal static bool Enabled = true;
    internal static List<StopChecker> Checkers = new List<StopChecker>();

    private readonly ITextBuffer _tb;
    private readonly IVsStatusbar _status;
    private readonly DispatcherTimer _timer;
    private readonly ITextStructureNavigator _tsn;
    private readonly ITagAggregator<ProgressGlyphTag> _agg;
    private readonly List<int> _changesSinceLastSuccessfulRun;
    private readonly List<DeadAnnotationTag> _deadAnnotations;

    private ProgressTagger _pt;
    private bool _lastRunFailed;
    private int _lastRunChangeCount;
    private bool _hasNeverRun = true;
    private StopChecker _currentStopper;

    public static bool IsCurrentlyActive { get; private set; }
    private static object _activityLock;
    private ITextSnapshot Snapshot => _tb.CurrentSnapshot;
    private bool LastRunFailedAndNoChangesMade => _lastRunFailed && _changesSinceLastSuccessfulRun.Count <= _lastRunChangeCount;
    private bool IsProgramValid => !_agg.GetTags(new SnapshotSpan(Snapshot, 0, Snapshot.Length)).Any() && RefactoringUtil.ProgramIsVerified(_tb);

    #endregion

    public DeadAnnotationTagger(ITextBuffer tb, IVsStatusbar status, ITextStructureNavigator tsn, ITagAggregator<ProgressGlyphTag> agg) {
      _activityLock = _activityLock ?? new object();

      _tb = tb;
      _agg = agg;
      _tsn = tsn;
      _status = status;
      _changesSinceLastSuccessfulRun = new List<int>();
      _deadAnnotations = new List<DeadAnnotationTag>();
      _tb.Properties.TryGetProperty(typeof(ProgressTagger), out _pt);
      
      _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(5) };
      _timer.Tick += IdleTick;
      _tb.Changed += BufferChangedInterrupt;
      _timer.Start();
    }
    
    #region events
    private void IdleTick(object s, EventArgs e) {
      if (!Monitor.TryEnter(_activityLock)) return;
      var safe = Enabled && !IsCurrentlyActive && IsProgressTaggerSafe() && IsProgramValid;
      Monitor.Exit(_activityLock);
      if (!safe) return;

      _currentStopper = new StopChecker();
      if (_hasNeverRun && !LastRunFailedAndNoChangesMade) {
        ProcessProgram();
      } else if (_changesSinceLastSuccessfulRun.Count > 0 && !LastRunFailedAndNoChangesMade) {
        ProcessSomeMembers();
      } else {
        NotifyStatusbar(DeadAnnotationStatus.NoChanges);
      }
    }

    private void BufferChangedInterrupt(object s, TextContentChangedEventArgs e) {
      if (IsCurrentlyActive) {
        lock (_activityLock) {
          IsCurrentlyActive = false;
          _currentStopper.Stop = true;
        }
      } else {
        _timer.Stop();
        _timer.Start();
      }
      foreach (var change in e.Changes.ToList()) {
        _changesSinceLastSuccessfulRun.Add(change.NewPosition);
        _deadAnnotations.RemoveAll(tag => {
          var span = tag.TrackingReplacementSpan.GetSpan(e.After);
          return span.OverlapsWith(change.NewSpan);
        });
        var changeSpan = new SnapshotSpan(Snapshot, change.NewSpan);
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(changeSpan));
      }
    }

    public void Dispose() {
      _timer.Stop();
      _timer.Tick -= IdleTick;
      _tb.Changed -= BufferChangedInterrupt;
    }
    #endregion

    #region status
    private enum DeadAnnotationStatus {
      Started, Finished, UserInterrupt, DaryFail, DaryFailInvalid, NoChanges, NoProgram
    }

    private void NotifyStatusbar(DeadAnnotationStatus status) {
      var tid = Thread.CurrentThread.ManagedThreadId;
      string s;
      switch (status) {
        case DeadAnnotationStatus.Started:
          s = $"Dead code analysis started - #{tid}";
          break;
        case DeadAnnotationStatus.Finished:
          s = $"Dead code analysis finished - #{tid}";
          break;
        case DeadAnnotationStatus.UserInterrupt:
          s = $"Dead code analysis interrupted by user - #{tid}";
          break;
        case DeadAnnotationStatus.DaryFail:
          s = $"Dead code analysis interrupted - #{tid}";
          break;
        case DeadAnnotationStatus.DaryFailInvalid:
          s = $"Dead code analysis interrupted due to invalid program - #{tid}";
          break;
        case DeadAnnotationStatus.NoChanges:
          s = $"Dead code analysis not run as no changes - #{tid}";
          break;
        case DeadAnnotationStatus.NoProgram:
          s = $"Dead code analysis could not run as no program - #{tid}";
          break;
        default:
          throw new tcce.UnreachableException();
      }
      _status.SetText(s);
    }
    #endregion

    #region thread housekeeping
    private struct ThreadParams
    {
      public Program P;
      public ITextSnapshot S;
      public List<MemberDecl> M;
      public StopChecker Stop;
    }

    private void Begin() {
      lock (_activityLock) {
        Checkers.Add(_currentStopper);
        IsCurrentlyActive = true;
        _timer.Stop();
      }
    }

    private void Finish() {
      lock (_activityLock) {
        Checkers.Remove(_currentStopper);
        IsCurrentlyActive = false;
        _timer.Start();
      }
    }

    private bool IsProgressTaggerSafe() {
      if (_pt == null) {
        _tb.Properties.TryGetProperty(typeof(ProgressTagger), out _pt);
        if (_pt == null) return false;
      }
      if (!Monitor.TryEnter(_pt)) return false;
      var result = !_pt.verificationInProgress;
      Monitor.Exit(_pt);
      return result;
    }
    #endregion

    #region processing
    private void ProcessProgram() {
      Begin();
      var prog = RefactoringUtil.GetNewProgram(_tb);
      if (prog==null) {
        NotifyStatusbar(DeadAnnotationStatus.NoProgram);
        Finish();
        return;
      }
      var t = new Thread(ProcessProgramThreaded);
      t.Start(new ThreadParams{P= prog, S = Snapshot, Stop = _currentStopper});
    }

    private void ProcessProgramThreaded(object o) {
      NotifyStatusbar(DeadAnnotationStatus.Started);
      var prog = ((ThreadParams) o).P;
      var snap = ((ThreadParams) o).S;
      var stop = ((ThreadParams) o).Stop;
      var dary = new Dary.Dary(stop);
      List<DaryResult> results;
      _lastRunChangeCount = _changesSinceLastSuccessfulRun.Count;
      try {
        results = dary.ProcessProgram(prog);
      } catch (NotValidException) {
        _lastRunFailed = true;
        NotifyStatusbar(DeadAnnotationStatus.DaryFailInvalid);
        Finish();
        return;
      }
      _lastRunFailed = false;
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.UserInterrupt);
        Finish();
        return;
      }
      _hasNeverRun = false;
      _changesSinceLastSuccessfulRun.Clear();
      lock (_deadAnnotations) {
        _deadAnnotations.Clear();
        results.ForEach(x => ProcessValidResult(x, prog));
      }
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));
      NotifyStatusbar(DeadAnnotationStatus.Finished);
      Finish();
    }

    private void ProcessSomeMembers(){
      Begin();
      var p = RefactoringUtil.GetNewProgram(_tb);
      if (p == null) {
        NotifyStatusbar(DeadAnnotationStatus.NoProgram);
        Finish();
        return;
      }
      var notProcessedMembers = new List<MemberDecl>();
      var tld = RefactoringUtil.GetTld(p);
      _changesSinceLastSuccessfulRun.ForEach(i => {
        MemberDecl md;
        RefactoringUtil.GetMemberFromPosition(tld, i, out md);
        if (md != null && !notProcessedMembers.Contains(md)) notProcessedMembers.Add(md);
      });
      if (tld == null || notProcessedMembers.Count == 0) {
        NotifyStatusbar(DeadAnnotationStatus.NoChanges);
        Finish();
        return;
      }
      var t = new Thread(ProcessSomeMembersThreaded);
      t.Start(new ThreadParams { P = p, M = notProcessedMembers, S = Snapshot, Stop = _currentStopper });
    }

    private void ProcessSomeMembersThreaded(object o) {
      NotifyStatusbar(DeadAnnotationStatus.Started);
      var prog = ((ThreadParams)o).P;
      var snap = ((ThreadParams)o).S;
      var stop = ((ThreadParams)o).Stop;
      var mds = ((ThreadParams) o).M;
      var dary = new Dary.Dary(stop);
      List<DaryResult> results;
      _lastRunChangeCount = _changesSinceLastSuccessfulRun.Count;
      try {
        results = dary.ProcessMembers(prog, mds);
      } catch (NotValidException) {
        _lastRunFailed = true;
        NotifyStatusbar(DeadAnnotationStatus.DaryFailInvalid);
        Finish();
        return;
      }
      _lastRunFailed = false;
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.UserInterrupt);
        Finish();
        return;
      }
      _changesSinceLastSuccessfulRun.Clear();
      lock (_deadAnnotations) {
        foreach (var m in mds) {
          var mSpan = new SnapshotSpan(snap, m.BodyStartTok.pos, m.BodyEndTok.pos - m.BodyStartTok.pos);
          _deadAnnotations.RemoveAll(tag => tag.TrackingReplacementSpan.GetSpan(snap).OverlapsWith(mSpan));
          TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(mSpan));
        }
        _deadAnnotations.Clear();
        results.ForEach(x => ProcessValidResult(x, prog));
      }
      NotifyStatusbar(DeadAnnotationStatus.Finished);
      Finish();
    }
    #endregion

    #region process results

    private struct Positions {
      public readonly int WarnStart, WarnLength, ReplaceStart, ReplaceLength;

      public Positions(int ws, int wl, int rs, int rl) {
        WarnStart = ws;
        WarnLength = wl;
        ReplaceStart = rs;
        ReplaceLength = rl;
      }
      public Positions(int s, int l) {
        WarnStart = ReplaceStart = s;
        WarnLength = ReplaceLength =l;
      }
    }

    private void ProcessValidResult(DaryResult r, Program p) {
      var replacement = FindReplacement(r.Replace);
      var actualTokPos = FindOffsetSpecialPositions(r);
      var pos = ReplacementPositions(actualTokPos, r.Length, r.Replace!=null);
      var tag = new DeadAnnotationTag(Snapshot, pos.WarnStart, pos.WarnLength, pos.ReplaceStart, pos.ReplaceLength, replacement, r.TypeOfRemovable, p);
      _deadAnnotations.Add(tag);
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(Snapshot, actualTokPos, r.Length)));
    }

    private Positions ReplacementPositions(int tokPos, int tokLen, bool hasReplace) {
      var line = Snapshot.GetLineFromPosition(tokPos);
      var wordAtEndOfTag = _tsn.GetExtentOfWord(new SnapshotPoint(Snapshot, tokPos + tokLen)).Span;
      var finalTaggedSegment = _tsn.GetSpanOfNextSibling(wordAtEndOfTag).GetText();
      var trailingSemiBrace = finalTaggedSegment.LastOrDefault(x => x==';' || x == '}');
      var actualLength = trailingSemiBrace != new char() ? tokLen+1 : tokLen;
      if (hasReplace) return new Positions(tokPos, actualLength);

      var lineText = line.Extent.GetText();
      var trimmedLineLength = lineText.Trim().Length; 
      var replacementSpan = new SnapshotSpan(Snapshot, tokPos, actualLength).GetText();
      var linebreak = line.GetLineBreakText()[line.LineBreakLength-1];
      var taggedLines = replacementSpan.Split(linebreak);
      if(taggedLines.Length > 1 || trimmedLineLength > taggedLines[0].Length+1)
        return new Positions(tokPos, actualLength);
      
      var startOfLine = line.Start.Position;
      var offsetToStartOfTextInLine = lineText.Length - lineText.TrimStart().Length;
      var wholeLength = actualLength + offsetToStartOfTextInLine + line.LineBreakLength;
      return new Positions(tokPos, actualLength, startOfLine, wholeLength);
    }

    private int FindOffsetSpecialPositions(DaryResult r) {
      switch (r.TypeOfRemovable) {
        case "Assert Statement":
        case "Calc Statement":
        case "Lemma Call":
        return r.StartTok.pos;
        case "Decreases Expression":
        case "Invariant":
          break;
        default:
          throw new tcce.UnreachableException();
      }
      var start = _tsn.GetExtentOfWord(new SnapshotPoint(Snapshot, r.StartTok.pos));
      var prev = _tsn.GetSpanOfPreviousSibling(start.Span);
      var last = prev;
      while (prev.GetText() != "decreases" && prev.GetText() != "invariant") {
        prev = _tsn.GetSpanOfPreviousSibling(prev);
        if (prev == last) return r.StartTok.pos;
        last = prev;
      }
      return prev.Start.Position;
    }

    private static string FindReplacement(object replacement) {
      if (replacement == null) return "";
      var sr = new StringWriter();
      var pr = new Printer(sr);
      if (replacement is Statement) {
        var stmt = (Statement)replacement;
        pr.PrintStatement(stmt, 0);
      } else if (replacement is MaybeFreeExpression) {
        var mfe = (MaybeFreeExpression)replacement;
        pr.PrintExpression(mfe.E, mfe.IsFree);
      }
      return sr.ToString();
    }
    #endregion

    #region tagging
    public IEnumerable<ITagSpan<DeadAnnotationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if (spans.Count <= 0 || _deadAnnotations.Count <= 0) return new List<ITagSpan<DeadAnnotationTag>>();
      var activeSnapshot = spans[0].Snapshot;
      return from span in spans
        from tag in _deadAnnotations
        let tagSpan = tag.TrackingReplacementSpan.GetSpan(activeSnapshot)
        where span.OverlapsWith(tagSpan)
        select new TagSpan<DeadAnnotationTag>(tag.WarnSpan, tag);
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    #endregion
  }
}
