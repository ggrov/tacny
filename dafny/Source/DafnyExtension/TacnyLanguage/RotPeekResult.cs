using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
  //PeekResult* is the wpf side of things

  #region provider

  [Export(typeof(IPeekResultPresenter))]
  [ContentType("dafny")]
  [Name("ReadOnlyTacticPeekResultPresenter")]
  internal class RotPeekResultPresenter : IPeekResultPresenter //gets the PeekResultPresentation
  {
    public IPeekResultPresentation TryCreatePeekResultPresentation(IPeekResult result) {
      return new RotPeekResultPresentation(result);
    }
  }

  #endregion provider

  internal class RotPeekResult : IPeekResult //the result of querying an iPeekableItem fora given relationship
  {
    public void Dispose() {}

    public void NavigateTo(object data) {
      //just scroll it into view... ? wat? TODO
    }

    public IPeekResultDisplayInfo DisplayInfo => new PeekResultDisplayInfo("label", "tooltip", "title", "titletoolTip");
    public bool CanNavigateTo => true; //i think
    public Action<IPeekResult, object, object> PostNavigationCallback => (pr, o, data) => { }; //do we need to do anything after navigation
    public event EventHandler Disposed { add{} remove{} }
  }

  internal class RotPeekResultPresentation : IPeekResultPresentation //defines a wpf representation for a PeekableItem
  {
    private readonly IPeekResult _result;
    private TextBox _tb;

    public RotPeekResultPresentation(IPeekResult result) {
      _result = result;
    }
    // https://msdn.microsoft.com/en-us/library/microsoft.visualstudio.language.intellisense.ipeekresultpresentation.aspx
    public void Dispose() {
      _tb = null;
    }

    public bool TryOpen(IPeekResult otherResult) => _result==otherResult; //dont opn a new one if already open. true or false?

    public bool TryPrepareToClose() => true;

    public UIElement Create(IPeekSession session, IPeekResultScrollState scrollState) { //document viewer / text box/ whaetever goes here
      _tb = new TextBox {
        Text = "BCBF4EDC - C032 - 4946 - A448 - 15D7465EFD30",
        Height = 20,
        Background = Brushes.LightSalmon
      };
      return _tb;
    }

    public void ScrollIntoView(IPeekResultScrollState scrollState) {
      //surely this is the job of the textvie,w not the peek?
    }

    public IPeekResultScrollState CaptureScrollState() {
      return new RotPeekResultScrollState();
    }

    public void Close() {}

    public void SetKeyboardFocus() {
      _tb.Focus();
    }

    public bool CanSave(out string defaultPath) {
      defaultPath = null;
      return false;
    }

    public bool TrySave(bool saveAs) => true;

    public double ZoomLevel { get; set; }
    public bool IsDirty => false;
    public bool IsReadOnly => true;
    public event EventHandler<RecreateContentEventArgs> RecreateContent {add{} remove{}}
    public event EventHandler IsDirtyChanged {add{} remove{}}
  public event EventHandler IsReadOnlyChanged {add{} remove{}}
  }

  internal class RotPeekResultScrollState : IPeekResultScrollState //gets (scroll) state of results in a peek?
  {
    public void Dispose() {}

    public void RestoreScrollState(IPeekResultPresentation presentation) {
      //there is no scroll state to return to?
    }
  }

  internal class RotPeekResultSource : IPeekResultSource //Content-type Peek providers implement this to provide results of querying IPeekableItems?

  {
    private readonly string _relName;

    public RotPeekResultSource(string relationshipName) {
      _relName = relationshipName;
    }

    public void FindResults(string relationshipName, IPeekResultCollection resultCollection, 
      CancellationToken cancellationToken, IFindPeekResultsCallback callback)
    {
      if(relationshipName==_relName) resultCollection.Add(new RotPeekResult());
    }
  }
}