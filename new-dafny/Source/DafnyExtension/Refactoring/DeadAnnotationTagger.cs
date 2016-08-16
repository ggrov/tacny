using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Program = Microsoft.Dafny.Program;
using shorty;
using Type = System.Type;

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

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
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

      Func<ITagger<T>> taggerProperty = () => new DeadAnnotationTagger(buffer, status) as ITagger<T>;
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
    public object ToolTipContent => "This code can safely be removed";

    public readonly string Replacement;
    public readonly SnapshotSpan WarnSpan;
    public readonly SnapshotSpan ReplacementSpan;
    public readonly ITextSnapshot Snapshot;
    
    public DeadAnnotationTag(ITextSnapshot snapshot, int warnStart, int warnLength, int replaceStart, int replaceLength, string replacement) : base(Type) {
      Snapshot = snapshot;
      Replacement = replacement;
      WarnSpan = new SnapshotSpan(snapshot, warnStart, warnLength);
      ReplacementSpan = new SnapshotSpan(snapshot, replaceStart, replaceLength);
    }
  }
  #endregion

  internal class DeadCodeMenuProxy : IDeadCodeMenuProxy {
    public void RemoveDeadCode(IWpfTextView tv) {
      InvokeSuggestedAction(tv, typeof(RemoveDeadAnnotationSuggestedAction));
    }

    public void RemoveAllDeadCode(IWpfTextView tv) {
      InvokeSuggestedAction(tv, typeof(RemoveAllDeadAnnotationsSuggestedAction));
    }

    private static void InvokeSuggestedAction(ITextView tv, Type type) {
      DeadAnnotationSuggestedActionsSource dasap;
      if (!tv.TextBuffer.Properties.TryGetProperty(typeof(DeadAnnotationSuggestedActionsSource), out dasap)) return;
      var position = new SnapshotSpan(tv.Caret.Position.BufferPosition, 1);
      var acts = dasap.GetSuggestedActions(null, position, CancellationToken.None);
      var action = (from suggestedActs in acts
                    from suggestedAct in suggestedActs.Actions
                    where suggestedAct.GetType() == type
                    select suggestedAct).FirstOrDefault();
      action?.Invoke(CancellationToken.None);
    }

    public bool Toggle() {
      DeadAnnotationTagger.Checkers.ForEach(x => x.Stop=true);
      DeadAnnotationTagger.Enabled = !DeadAnnotationTagger.Enabled;
      return DeadAnnotationTagger.Enabled;
    }
  }

  internal class DeadAnnotationTagger : ITagger<DeadAnnotationTag>, IDisposable
  {
    internal static bool Enabled = true;
    internal static List<StopChecker> Checkers = new List<StopChecker>();

    private readonly ITextBuffer _tb;
    private readonly IVsStatusbar _status;
    private readonly DispatcherTimer _timer;
    private readonly List<int> _changesSinceLastRun;
    private readonly List<DeadAnnotationTag> _deadAnnotations;

    private ProgressTagger _pt;
    private bool _hasNeverRun = true;
    private StopChecker _currentStopper;

    public static bool IsCurrentlyActive { get; private set; }
    private static object _activityLock;

    public DeadAnnotationTagger(ITextBuffer tb, IVsStatusbar status) {
      _activityLock = _activityLock ?? new object();

      _tb = tb;
      _status = status;
      _changesSinceLastRun = new List<int>();
      _deadAnnotations = new List<DeadAnnotationTag>();
      _tb.Properties.TryGetProperty(typeof(ProgressTagger), out _pt);
      
      _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(10) };
      _timer.Tick += IdleTick;
      _tb.Changed += BufferChangedInterrupt;
      _timer.Start();
    }
    
    #region events
    private void IdleTick(object s, EventArgs e) {
      lock (_activityLock)
      {
        if (!Enabled || IsCurrentlyActive || !IsProgressTaggerSafe()) return;
      }
      _currentStopper = new StopChecker();
      if (_hasNeverRun) {
        ProcessProgram();
      } else if (_changesSinceLastRun.Count > 0) {
        ProcessSomeMembers();
      }
    }

    private void BufferChangedInterrupt(object s, TextContentChangedEventArgs e) {
      lock (_activityLock)
      {
        if (IsCurrentlyActive) {
          IsCurrentlyActive = false;
          _currentStopper.Stop = true;
        } else {
          _timer.Stop();
          _timer.Start();
        }
      }
      if (_hasNeverRun) return;
      foreach (var change in e.Changes.ToList()) {
        _changesSinceLastRun.Add(change.NewPosition);
        _deadAnnotations.RemoveAll(x => {
          var originalSpan = new SnapshotSpan(x.ReplacementSpan.Start, x.ReplacementSpan.End);
          return originalSpan.OverlapsWith(change.OldSpan) || originalSpan.OverlapsWith(change.NewSpan);
        });
      }
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_tb.CurrentSnapshot, 0, _tb.CurrentSnapshot.Length)));
    }

    public void Dispose() {
      _timer.Stop();
      _timer.Tick -= IdleTick;
      _tb.Changed -= BufferChangedInterrupt;
    }
    #endregion

    #region status
    private enum DeadAnnotationStatus {
      Started, Finished, UserInterrupt, DaryFail
    }

    private void NotifyStatusbar(DeadAnnotationStatus status) {
      var tid = Thread.CurrentThread.ManagedThreadId;
      string s;
      switch (status)
      {
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
        default:
          throw new cce.UnreachableException();
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
      Program program;
      if (!RefactoringUtil.GetExistingProgram(_tb, out program)) {
        Finish();
        return;
      }
      var t = new Thread(ProcessProgramThreaded);
      t.Start(new ThreadParams{P= program, S = _tb.CurrentSnapshot, Stop = _currentStopper});
    }

    private void ProcessProgramThreaded(object o) {
      NotifyStatusbar(DeadAnnotationStatus.Started);
      var prog = ((ThreadParams) o).P;
      var snap = ((ThreadParams) o).S;
      var stop = ((ThreadParams) o).Stop;
      var dary = new Dary(stop);
      List<DaryResult> results;
      try {
        results = dary.ProcessProgram(prog);
      } catch (Exception) {
        NotifyStatusbar(DeadAnnotationStatus.DaryFail);
        Finish();
        return;
      }
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.UserInterrupt);
        Finish();
        return;
      }
      _hasNeverRun = false;
      lock (_deadAnnotations) {
        results.ForEach(ProcessValidResult);
      }
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));
      NotifyStatusbar(DeadAnnotationStatus.Finished);
      Finish();
    }

    private void ProcessSomeMembers(){
      Begin();
      Program p;
      if (!RefactoringUtil.GetExistingProgram(_tb, out p)) {
        Finish();
        return;
      }
      var notProcessedMembers = new List<MemberDecl>();
      var tld = p?.DefaultModuleDef.TopLevelDecls.FirstOrDefault() as DefaultClassDecl;
      _changesSinceLastRun.ForEach(i => {
        MemberDecl md;
        RefactoringUtil.GetMemberFromPosition(tld, i, out md);
        if (md != null && !notProcessedMembers.Contains(md)) notProcessedMembers.Add(md);
      });
      if (tld == null || notProcessedMembers.Count == 0) {
        Finish();
        return;
      }
      var t = new Thread(ProcessSomeMembersThreaded);
      t.Start(new ThreadParams { P = p, M = notProcessedMembers, S = _tb.CurrentSnapshot, Stop = _currentStopper });
    }

    private void ProcessSomeMembersThreaded(object o) {
      NotifyStatusbar(DeadAnnotationStatus.Started);
      var prog = ((ThreadParams)o).P;
      var snap = ((ThreadParams)o).S;
      var stop = ((ThreadParams)o).Stop;
      var mds = ((ThreadParams) o).M;
      var dary = new Dary(stop);
      List<DaryResult> results;
      try {
        results = dary.ProcessMembers(prog, mds);
      } catch (Exception) {
        NotifyStatusbar(DeadAnnotationStatus.DaryFail);
        Finish();
        return;
      }
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.UserInterrupt);
        Finish();
        return;
      }
      _changesSinceLastRun.Clear();
      lock (_deadAnnotations)
      {
        results.ForEach(ProcessValidResult);
      }
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));
      NotifyStatusbar(DeadAnnotationStatus.Finished);
      Finish();
    }

    private void ProcessValidResult(DaryResult r) {
      var replacement = FindReplacement(r.Replace);
      var repPos = ReplacementPositions(r);
      var tag = new DeadAnnotationTag(_tb.CurrentSnapshot, r.StartTok.pos, r.Length, repPos.Item1, repPos.Item2, replacement);
      _deadAnnotations.Add(tag);
    }

    private Tuple<int, int> ReplacementPositions(DaryResult r) {
      var replaceStart = r.StartTok.pos;
      var replaceLength = r.Length;
      if (r.Replace != null) return new Tuple<int, int>(replaceStart, replaceLength);

      var line = _tb.CurrentSnapshot.GetLineFromPosition(replaceStart);
      replaceStart = line.Start;
      replaceLength = line.LengthIncludingLineBreak;
      return new Tuple<int, int>(replaceStart, replaceLength);
    }

    private static string FindReplacement(object replacement) {
      if (replacement == null) return "";
      var sr = new StringWriter();
      var pr = new Printer(sr);
      if (replacement is Statement) {
        var stmt = (Statement)replacement;
        pr.PrintStatement(stmt, 4);
      } else if (replacement is MaybeFreeExpression) {
        var mfe = (MaybeFreeExpression)replacement;
        pr.PrintExpression(mfe.E, mfe.IsFree); //todo test this one
      }
      return sr.ToString();
    }
    #endregion

    #region tagging
    public IEnumerable<ITagSpan<DeadAnnotationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if (spans.Count > 0 && _deadAnnotations.Count > 0 && spans[0].Snapshot == _deadAnnotations[0].Snapshot) {
        return from span in spans
          from tag in _deadAnnotations
          where span.OverlapsWith(tag.ReplacementSpan)
          select new TagSpan<DeadAnnotationTag>(tag.WarnSpan, tag);
      }
      return new List<ITagSpan<DeadAnnotationTag>>();
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    #endregion
  }
}
