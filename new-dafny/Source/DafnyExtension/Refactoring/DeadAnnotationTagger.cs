using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using DafnyLanguage.DafnyMenu;
using Microsoft.Boogie;
using Microsoft.Dafny;
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
    private IServiceProvider _isp;

    [Import]
    private ITextDocumentFactoryService _tdf;

    [Import]
    private IClassificationTypeRegistryService _ctr;
    
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
      return new DeadAnnotationTagger(buffer, _isp, _tdf, _ctr) as ITagger<T>;
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
    internal static ClassificationTypeDefinition UserType;
  }
  #endregion mefprovider

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
  #endregion tags

  #region dummyinterface
  /*internal class DummyDeadAnnotationRemover
  {
    /*
     * List<> results = await ProcessMember
     * 
     * progress margin - later
     *

    private static void BeBusy(Action<uint, uint> notify = null) {
      uint t = 1;
      for (uint i = 0; i < t; i++) {
        notify?.Invoke(i, t);
        Thread.Sleep(100);
      }
    }

    // Returns an object representing a member that has been processed
    // null if failure
    public DarResult ProcessMember(MemberDecl member, Program program) { //TODO multiple members
      BeBusy();
      return new DarResult();
    }

    // Returns tuple
    // Item 1 = completed members
    // Item 2 = uncompleted members
    public Tuple<List<DarResult>, List<MemberDecl>> ProcessProgram(Program program, Action<uint, uint> notify) {
      BeBusy(notify);
      return new Tuple<List<DarResult>, List<MemberDecl>>(new List<DarResult> {new DarResult()}, new List<MemberDecl>());
    }

    // Returns if stop was successful
    public bool Stop() { //todo assume nothing ran, nothing worked
      return false;
    }
  }

  internal class DummyToken : Token
  {
    public int Len;
    public DummyToken(int p, int c, int l, int length) {
      pos = p;
      line = l;
      col = c;
      Len = length;
    }
  }

  internal class DarResult
  {
    public readonly List<Token> Removable;

    public DarResult() {
      Removable = new List<Token> {new DummyToken(0, 0, 0, 4), new DummyToken(35, 0, 3, 5)};
    }
  }*/
  #endregion dummyinterface

  //on method edit, get rid of any 'dead' annotations


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
    //private readonly ErrorListProvider _elp;
    private readonly IClassificationType _type;
    private readonly ITextDocumentFactoryService _tdf;

    private readonly List<DeadAnnotationTag> _deadAnnotations;
    private readonly List<Tuple<MemberDecl, Program>> _notProcessedMembers;
    //private ITextSnapshot _lastRunSnapshot;
    private bool _hasNeverRun;
    private StopChecker _currentStopper;

    public static bool IsCurrentlyActive { get; private set; }

    public DeadAnnotationTagger(ITextBuffer tb, IServiceProvider isp, ITextDocumentFactoryService tdf, IClassificationTypeRegistryService ctr) {
      //_elp = new ErrorListProvider(isp);
      _tb = tb;
      _tdf = tdf;
      _type = ctr.GetClassificationType("Dead Annotation");
      _status = (IVsStatusbar) isp.GetService(typeof(IVsStatusbar));

      _notProcessedMembers = new List<Tuple<MemberDecl, Program>>();
      _deadAnnotations = new List<DeadAnnotationTag>();
      _hasNeverRun = true;
      IsCurrentlyActive = false;

      _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(5) };
      _timer.Tick += Activate;
      _timer.Start();
    }
    
    private struct ThreadParams
    {
      public Program P;
      public ITextSnapshot S;
      public Tuple<MemberDecl, Program> T;
      public StopChecker Stop;
    }

    private string GetFilename(ITextBuffer tb = null) {
      if (tb == null) tb = _tb;
      ITextDocument doc = null;
      _tdf?.TryGetTextDocument(tb, out doc);
      return doc?.FilePath;
    }
    
    public void Activate(object s, EventArgs e) {
      if (!Enabled || IsCurrentlyActive) return;
      
      _currentStopper = new StopChecker(); //assuming if we got this far that any previosuly running dary has been stopped
      if (_hasNeverRun) {
        var file = GetFilename();
        if (string.IsNullOrEmpty(file)) return;
        var driver = new DafnyDriver(_tb, file);
        var program = driver.ProcessResolution(true);
        //if (program == null) return; //todo re-enable when we care about having a program
        InitialRun(program);
      }
      if (_notProcessedMembers.Count > 0) { 
        //TODO is there an efficiency consideration regarding just re-running for whole program if 85% havent yet been run
        var front = _notProcessedMembers[0];
        _notProcessedMembers.Remove(front);
        ProcessOneMember(front);
      }
      FindChangedMembers();
    }
    
    private void FindChangedMembers() {
      //TODO for now, do all or nothing.
      // var current = _tb.CurrentSnapshot; //TODO 
      //do whatever progress margin does to tag changed lines
      //get the members for these changed lines
      //foreach member changed, add it to notProcessed
      if (_notProcessedMembers.Count > 0) Activate(this, new EventArgs());
    }

    private void NotifyStatus(uint completed, uint total) {
      var tid = Thread.CurrentThread.ManagedThreadId;
      _status.SetText(completed == total
        ? $"Dead code analysis complete - #{tid}"
        : $"Analysing code for dead annotations ({completed}/{total}) - #{tid}");
    }

    private void InitialRun(Program program)
    {
      IsCurrentlyActive = true;
      NotifyStatus(0, 1);
      //_lastRunSnapshot = _tb.CurrentSnapshot;
      var t = new Thread(InitialRunThreaded);
      t.Start(new ThreadParams{P= program, S = _tb.CurrentSnapshot, Stop = _currentStopper});
    }

    private void InitialRunThreaded(object o) {
      var prog = ((ThreadParams) o).P;
      var snap = ((ThreadParams) o).S;
      var stop = ((ThreadParams) o).Stop;
      var dary = new Dary(stop);
      var results = dary.ProcessProgram(prog);
      _hasNeverRun = false;
      //_notProcessedMembers.Clear();
      IsCurrentlyActive = false;
      NotifyStatus(1, 1);
      results.ForEach(ProcessValidResult);
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length))); //thread stop
    }

    private void ProcessOneMember(Tuple<MemberDecl, Program> memberdata) {
      IsCurrentlyActive = true;
      NotifyStatus(0, 1);
      //_lastRunSnapshot = _tb.CurrentSnapshot;
      var t = new Thread(ProcessOneMemberThreaded);
      t.Start(new ThreadParams {T = memberdata, S = _tb.CurrentSnapshot, Stop = _currentStopper}); //TODO make sure using right snapshot
    }

    private void ProcessOneMemberThreaded(object o) {
      var tupledata = ((ThreadParams) o).T;
      var original = tupledata.Item1;
      var prog = tupledata.Item2;
      var snap = ((ThreadParams)o).S;
      var stop = ((ThreadParams)o).Stop;
      var dary = new Dary(stop);
      var result = dary.ProcessMembers(prog, new List<MemberDecl> {original});
      _notProcessedMembers.Remove(tupledata);
      IsCurrentlyActive = false;
      NotifyStatus(1, 1);
      ProcessValidResult(result[0]);
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));//todo only notify of updated lines
    }

    private void ProcessValidResult(DaryResult r) {
      //if()
      //_deadAnnotations.Add(
      //  new DeadAnnotationTag(_tb.CurrentSnapshot, r.StartPos, r.Length, r., t.col, GetFilename(_tb), _type)
      //);
    }

    private void Interrupt() {//TODO
      if (!IsCurrentlyActive) return;
      //_ddar.Stop();
      IsCurrentlyActive = false;
      NotifyStatus(1, 1);
    }

    public void Dispose() {
      //_ddar.Stop();
      _timer.Tick -= Activate; //TODO remove all error tasks here
      //var toRemove = (from object task in _elp.Tasks
      //                let dat = task as DeadAnnotationTask
      //                where dat != null
      //                where dat.Document == GetFilename()
      //                select task).Cast<ErrorTask>().ToList();
      //toRemove.ForEach(_elp.Tasks.Remove);
    }
    
    //private void AddElpTask(ErrorTask t) {
    //   var found = (from object task in _elp.Tasks //TODO only keep newest errors
    //                select task as DeadAnnotationTask).Any(errTask => 
    //                errTask?.Column == t.Column
    //                && errTask.Line == t.Line
    //                && errTask.Document == t.Document);
    //   if(!found) _elp.Tasks.Add(t);
    //}

    public IEnumerable<ITagSpan<DeadAnnotationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      //_deadAnnotations.ForEach(item => AddElpTask(item.Error));
      return from span in spans
        from tag in _deadAnnotations
        where span.OverlapsWith(tag.Span)
        select new TagSpan<DeadAnnotationTag>(tag.Span, tag);
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
