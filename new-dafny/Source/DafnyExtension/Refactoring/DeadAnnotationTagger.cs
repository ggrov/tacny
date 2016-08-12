using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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

      var datype = Ctr.GetClassificationType("Dead Annotation");
      Func<ITagger<T>> taggerProperty = () => new DeadAnnotationTagger(buffer, Isp, Tdf, datype) as ITagger<T>;
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
  internal sealed class DeadAnnotationTask : ErrorTask
  {
    public DeadAnnotationTask(int line, int col, string file) {
      Category = TaskCategory.BuildCompile;
      ErrorCategory = TaskErrorCategory.Warning;
      Text = "This annotation can safely be removed";
      Line = line;
      Column = col;
      Document = file;
    }
  }

  internal sealed class DeadAnnotationTag : ClassificationTag, IErrorTag
  {
    public string ErrorType => PredefinedErrorTypeNames.OtherError;
    public object ToolTipContent => "This annotation can safely be removed";

    public int Line => Error.Line;
    public int Col => Error.Column;
    public readonly DeadAnnotationTask Error;
    public readonly SnapshotSpan Span;
    public readonly ITextSnapshot Snap;

    public DeadAnnotationTag(ITextSnapshot snap, int start, int length, int line, int col, string file, IClassificationType type) : base(type) {
      Snap = snap;
      Span = new SnapshotSpan(snap, start, length);
      Error = new DeadAnnotationTask(line, col, file);
    }
  }
  #endregion

  internal class DeadCodeMenuProxy : IDeadCodeMenuProxy
  {
    public void RemoveDeadCode(IWpfTextView tv) {
      throw new NotImplementedException();
      //get position
      //look for spans under that position
      //remove them
      //update everything
    }

    public void RemoveAllDeadCode(IWpfTextView tv) {
      throw new NotImplementedException();
      //get all spans in snapshot for tv
      //remove them
      //update everything
    }

    public bool Toggle() {
      DeadAnnotationTagger.Enabled = !DeadAnnotationTagger.Enabled;
      return DeadAnnotationTagger.Enabled;
    }
  }

  internal class DeadAnnotationTagger : ITagger<DeadAnnotationTag>, IDisposable
  {
    internal static bool Enabled = true;

    private readonly ITextBuffer _tb;
    private readonly IVsStatusbar _status;
    private readonly DispatcherTimer _timer;
    private readonly IClassificationType _type;
    private readonly List<DeadAnnotationTag> _deadAnnotations;

    private ProgressTagger _pt;
    private bool _hasNeverRun = true;
    private StopChecker _currentStopper;
    private List<int> _changesSinceLastRun = new List<int>();

    public static bool IsCurrentlyActive { get; private set; }
    private static object _activityLock;

    public DeadAnnotationTagger(ITextBuffer tb, IServiceProvider isp, ITextDocumentFactoryService tdf, IClassificationType type) {
      RefactoringUtil.Tdf = RefactoringUtil.Tdf ?? tdf;
      _activityLock = _activityLock ?? new object();

      _tb = tb;
      _type = type;
      _deadAnnotations = new List<DeadAnnotationTag>();
      _status = (IVsStatusbar) isp.GetService(typeof(IVsStatusbar));
      _tb.Properties.TryGetProperty(typeof(ProgressTagger), out _pt);
      
      _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(10) };
      _timer.Tick += IdleTick;
      _tb.Changed += BufferChangedInterrupt;
      _timer.Start();
    }
    
    #region events
    private void IdleTick(object s, EventArgs e) {
      if (!Enabled || IsCurrentlyActive || !IsProgressTaggerSafe()) return;
      
      _currentStopper = new StopChecker();
      if (_hasNeverRun) {
        ProcessProgram();
      }
      //TODO is there an efficiency consideration regarding just re-running for whole program if 85% havent yet been run
      if (_changesSinceLastRun.Count > 0) {
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
          _timer.Start();
        }
      }
      _changesSinceLastRun = e.Changes.Select(c => c.NewPosition).ToList();
      //todo on method edit, get rid of any 'dead' annotations
    }
    public void Dispose() {
      _timer.Stop();
      _timer.Tick -= IdleTick;
      _tb.Changed -= BufferChangedInterrupt;
    }
    #endregion

    #region status
    private enum DeadAnnotationStatus {
      Started, Finished, Interrupted
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
        case DeadAnnotationStatus.Interrupted:
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
        IsCurrentlyActive = true;
        _timer.Stop();
      }
    }

    private void Finish() {
      lock (_activityLock) {
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
      Monitor.Exit(_pt); //todo this may not work
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
      var results = dary.ProcessProgram(prog);
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.Interrupted);
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

    private void ProcessSomeMembers() {
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
        if (md != null) notProcessedMembers.Add(md);
      });
      if (notProcessedMembers.Count == 0) {
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
      var results = dary.ProcessMembers(prog, mds);
      if (_currentStopper.Stop) {
        NotifyStatusbar(DeadAnnotationStatus.Interrupted);
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
      //var type = r.TypeOfRemovable; //assume get rid of everything for now
      /*TODO if removing entire line, we also want to + 1 for the semi
       * also, we may need to remove indentation and newlines.
       line_as_text.Regex([.\t\n\r]*([\S]+)[.\t\n\r]*).group 1
       */
      var tag = new DeadAnnotationTag(_tb.CurrentSnapshot, r.StartPos, r.Length, r.Token.line, r.Token.col, r.Token.filename, _type);
      _deadAnnotations.Add(tag);
    }
    #endregion

    #region tagging
    public IEnumerable<ITagSpan<DeadAnnotationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if (spans.Count > 0 && _deadAnnotations.Count > 0 && spans[0].Snapshot == _deadAnnotations[0].Snap) {
        return from span in spans
          from tag in _deadAnnotations
          where span.OverlapsWith(tag.Span) //? not the right span.
          select new TagSpan<DeadAnnotationTag>(tag.Span, tag);
      }
      return new List<ITagSpan<DeadAnnotationTag>>();
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    #endregion
  }
}
