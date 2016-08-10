using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Program = Microsoft.Dafny.Program;

namespace DafnyLanguage.TacnyLanguage
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
  internal class DummyDeadAnnotationRemover
  {
    // Returns an object representing a member that has been processed
    // null if failure
    public DarResult ProcessMember(MemberDecl member, Program program) {
      return new DarResult();
    }

    // Returns tuple
    // Item 1 = completed members
    // Item 2 = uncompleted members
    public Tuple<List<DarResult>, List<MemberDecl>> ProcessProgram(Program program) {
      return new Tuple<List<DarResult>, List<MemberDecl>>(new List<DarResult> {new DarResult()}, new List<MemberDecl>());
    }

    // Returns if stop was successful
    public bool Stop() {
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
  }
  #endregion dummyinterface

  internal class DeadAnnotationTagger : ITagger<DeadAnnotationTag>, IDisposable
  {
    private readonly ITextBuffer _tb;
    private readonly IVsStatusbar _status;
    private readonly DispatcherTimer _timer;
    //private readonly ErrorListProvider _elp;
    private readonly IClassificationType _type;
    private readonly DummyDeadAnnotationRemover _ddar;
    private readonly ITextDocumentFactoryService _tdf;

    private readonly List<DeadAnnotationTag> _deadAnnotations;
    private readonly List<MemberDecl> _notProcessedMembers;
    //private ITextSnapshot _lastRunSnapshot;
    private bool _hasNeverRun;
    private bool _active;

    public DeadAnnotationTagger(ITextBuffer tb, IServiceProvider isp, ITextDocumentFactoryService tdf, IClassificationTypeRegistryService ctr) {
      _ddar = new DummyDeadAnnotationRemover();
      //_elp = new ErrorListProvider(isp);
      _tb = tb;
      _tdf = tdf;
      _type = ctr.GetClassificationType("Dead Annotation");
      _status = (IVsStatusbar) isp.GetService(typeof(IVsStatusbar));

      _notProcessedMembers = new List<MemberDecl>();
      _deadAnnotations = new List<DeadAnnotationTag>();
      _hasNeverRun = true;
      _active = false;

      _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(10) };
      _timer.Tick += Activate;
      _timer.Start();
    }
    
    private class ThreadParams
    {
      public ThreadParams(Program p, ITextSnapshot s, MemberDecl m = null) {
        P = p;
        M = m;
        S = s;
      }

      public readonly Program P;
      public readonly MemberDecl M;
      public readonly ITextSnapshot S;
    }

    private string GetFilename(ITextBuffer tb = null) {
      if (tb == null) tb = _tb;
      ITextDocument doc = null;
      _tdf?.TryGetTextDocument(tb, out doc);
      return doc?.FilePath;
    }
    
    public void Activate(object s, EventArgs e) {
      _timer.Stop(); //TODO
      var file = GetFilename();
      if (string.IsNullOrEmpty(file)) return;
      var driver = new DafnyDriver(_tb, file);
      var program = driver.ProcessResolution(true);
      if (program == null) return;
      
      if (_active) return;
      if (_hasNeverRun) InitialRun(program);
      if (_notProcessedMembers.Count > 0) { 
        //TODO is there an efficiency consideration regarding just re-running for whole program if 85% havent yet been run
        var front = _notProcessedMembers[0];
        _notProcessedMembers.Remove(front);
        ProcessOneMember(program, front);
      }
      FindChangedMembers();
    }
    
    private void FindChangedMembers() {
     // var current = _tb.CurrentSnapshot; //TODO 
      //do whatever progress margin does to tag changed lines
      //get the members for these changed lines
      //foreach member changed, add it to notProcessed
      if(_notProcessedMembers.Count > 0) Activate(this, new EventArgs());
    }

    private void NotifyStatus(bool active) {
      _active = active;
      var msg = active ? "Now checking for removable annotations" : "Annotation checking finished";
      _status.SetText(msg);
    }
    
    private void InitialRun(Program program) {
      NotifyStatus(true);
      //_lastRunSnapshot = _tb.CurrentSnapshot;
      var t = new Thread(InitialRunThreaded);
      t.Start(new ThreadParams(program, _tb.CurrentSnapshot));
    }

    private void InitialRunThreaded(object o) {
      var prog = ((ThreadParams) o).P;
      var snap = ((ThreadParams) o).S;
      var results = _ddar.ProcessProgram(prog);
      _hasNeverRun = false;
      _notProcessedMembers.Clear();
      _notProcessedMembers.AddRange(results.Item2);
      NotifyStatus(false);
      results.Item1.ForEach(ProcessValidResult);
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));
    }

    private void ProcessOneMember(Program p, MemberDecl m) {
      NotifyStatus(true);
      //_lastRunSnapshot = _tb.CurrentSnapshot;
      var t = new Thread(ProcessOneMemberThreaded);
      t.Start(new ThreadParams(p, _tb.CurrentSnapshot, m));
    }

    private void ProcessOneMemberThreaded(object o) {
      var prog = ((ThreadParams)o).P;
      var snap = ((ThreadParams)o).S;
      var original = ((ThreadParams)o).M;
      var result = _ddar.ProcessMember(original, prog);
      _notProcessedMembers.Remove(original);
      NotifyStatus(false);
      ProcessValidResult(result);
      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snap, 0, snap.Length)));
    }

    private void ProcessValidResult(DarResult m) {
      m.Removable.ForEach(t => _deadAnnotations.Add(
        new DeadAnnotationTag(_tb.CurrentSnapshot, t.pos, ((DummyToken)t).Len, t.line, t.col, GetFilename(_tb), _type)
      ));
    }

    public void Dispose() {
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
      return _deadAnnotations.Select(tag => new TagSpan<DeadAnnotationTag>(tag.Span, tag));
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
