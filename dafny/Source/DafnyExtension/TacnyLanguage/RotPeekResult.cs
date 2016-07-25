using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
  #region provider

  [Export(typeof(IPeekResultPresenter))]
  [ContentType("dafny")]
  [Name("ReadOnlyTacticPeekResultPresenter")]
  internal class RotPeekResultPresenter : IPeekResultPresenter
  {
    public IPeekResultPresentation TryCreatePeekResultPresentation(IPeekResult result) {
      return new RotPeekResultPresentation(result);
    }
  }

  #endregion provider

  internal class RotPeekResult : IPeekResult
  {
    public RotPeekResult(string expandedTactic)
    {
      ExpandedTactic = expandedTactic;
    }

    public string ExpandedTactic;
    private const string Title = "Expanded Tactics";
    private const string Tip = "Text previewing expanded tactics";
    
    public void Dispose() {}

    public void NavigateTo(object data) {} //TODO maybe

    public IPeekResultDisplayInfo DisplayInfo => new PeekResultDisplayInfo(Title, Tip, Title, Tip);
    public bool CanNavigateTo => false; //TODO i think
    public Action<IPeekResult, object, object> PostNavigationCallback => (pr, o, data) => { }; //TODO do we need to do anything after navigation
    public event EventHandler Disposed { add{} remove{} }
  }

  internal class RotPeekResultPresentation : IPeekResultPresentation
  {
    private readonly string _expandedTactic;
    private TextBox _tb;

    public RotPeekResultPresentation(IPeekResult result)
    {
      var rotResult = result as RotPeekResult;
      _expandedTactic = rotResult?.ExpandedTactic ?? "Failed to Expand Tactic";
    }

    public void Dispose() {
      _tb = null;
    }

    public bool TryOpen(IPeekResult otherResult)
    {
      var other = otherResult as RotPeekResult;
      return _expandedTactic==other?.ExpandedTactic;
    } 

    public bool TryPrepareToClose() => true;

    public UIElement Create(IPeekSession session, IPeekResultScrollState scrollState) {
      _tb = new TextBox {
        Text = _expandedTactic,
        Background = Brushes.AliceBlue,
        IsReadOnly = true
      };
      _tb.MinHeight = _tb.Text.Split('\n').Length + 1 * _tb.FontSize;
      return _tb;
    }

    public void ScrollIntoView(IPeekResultScrollState scrollState) {
      //TODO surely this is the job of the textvie,w not the peek?
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
    public event EventHandler<RecreateContentEventArgs> RecreateContent {add{} remove{}} //TODO
    public event EventHandler IsDirtyChanged {add{} remove{}}
    public event EventHandler IsReadOnlyChanged {add{} remove{}}
  }

  internal class RotPeekResultScrollState : IPeekResultScrollState //TODO  gets (scroll) state of results in a peek?
  {
    public void Dispose() {}

    public void RestoreScrollState(IPeekResultPresentation presentation) {
      //TODO there is no scroll state to return to?
    }
  }

  internal class RotPeekResultSource : IPeekResultSource
  {
    private readonly string _relName, _expandedTactic;

    public RotPeekResultSource(string relationshipName, string expandedTactic)
    {
      _relName = relationshipName;
      _expandedTactic = expandedTactic;
    }

    public void FindResults(string relationshipName, IPeekResultCollection resultCollection, 
      CancellationToken cancellationToken, IFindPeekResultsCallback callback)
    {
      if(relationshipName==_relName) resultCollection.Add(new RotPeekResult(_expandedTactic));
    }
  }
}