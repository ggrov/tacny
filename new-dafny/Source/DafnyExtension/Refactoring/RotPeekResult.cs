using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.Refactoring
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
    public RotPeekResult(string expandedTactic, string activeTactic)
    {
      ExpandedTactic = expandedTactic;
      _activeTactic = activeTactic;
    }

    public string ExpandedTactic;
    private readonly string _activeTactic;
    private const string Title = "Expanded Tactic";
    private const string Tip = "Text previewing ";

    public IPeekResultDisplayInfo DisplayInfo => new PeekResultDisplayInfo(Title, Tip+Title, _activeTactic, Tip+_activeTactic);
    
    public void Dispose() {}
    public void NavigateTo(object data) {}
    public bool CanNavigateTo => false;
    public Action<IPeekResult, object, object> PostNavigationCallback => (pr, o, data) => { };
    public event EventHandler Disposed { add{} remove{} }
  }

  internal class RotPeekResultPresentation : IPeekResultPresentation, IDesiredHeightProvider
  {
    private string _expandedTactic;
    private IPeekSession _session;
    private TextBox _tb;
    private const double ExpectedFontSize = 12.0;
    private const double PeekBorders = 24.0;

    public RotPeekResultPresentation(IPeekResult result)
    {
      var rotResult = result as RotPeekResult;
      _expandedTactic = rotResult?.ExpandedTactic ?? "Failed to Expand Tactic";
    }

    private double TextHeight => (_expandedTactic.Split('\n').Length + 1) * ExpectedFontSize;
    public double DesiredHeight => TextHeight + PeekBorders;

    public UIElement Create(IPeekSession session, IPeekResultScrollState scrollState) {
      _session = session;
      DafnyClassifier.DafnyMenuPackage.TacnyMenuProxy.AddUpdaterForRot(_session, Recalculate);
      _tb = new TextBox {
        Text = _expandedTactic,
        Background = Brushes.AliceBlue,
        IsReadOnly = true,
        FontFamily = new FontFamily("Consolas"),
        FontSize = ExpectedFontSize,
        MinHeight = TextHeight
      };
      return _tb;
    }

    private void Recalculate(string newExpanded)
    {
      _tb.Dispatcher.Invoke(() =>
      {
        if (newExpanded == null) {
          _session.Dismiss();
          return;
        }
        _tb.Text = _expandedTactic = newExpanded;
        _tb.MinHeight = TextHeight;
        DesiredHeightChanged?.Invoke(this, EventArgs.Empty);
      });
    }

    public bool TryOpen(IPeekResult otherResult)
    {
      var other = otherResult as RotPeekResult;
      return _expandedTactic==other?.ExpandedTactic;
    } 
    
    public bool CanSave(out string defaultPath) {
      defaultPath = null;
      return false;
    }
    
    public void Close() {
      DafnyClassifier.DafnyMenuPackage.TacnyMenuProxy.ClearPeekSession(_session);
    }

    public IPeekResultScrollState CaptureScrollState() => new RotPeekResultScrollState();
    public void SetKeyboardFocus() => _tb.Focus();
    public void Dispose() {}
    public void ScrollIntoView(IPeekResultScrollState scrollState) {}

    public bool TrySave(bool saveAs) => true;
    public bool TryPrepareToClose() => true;
    public bool IsDirty => false;
    public bool IsReadOnly => true;

    public double ZoomLevel { get; set; }
    public event EventHandler<RecreateContentEventArgs> RecreateContent { add { } remove { } }
    public event EventHandler IsDirtyChanged {add{} remove{}}
    public event EventHandler IsReadOnlyChanged {add{} remove{}}
    public event EventHandler<EventArgs> DesiredHeightChanged;
  }

  internal class RotPeekResultScrollState : IPeekResultScrollState
  {
    public void Dispose() {}
    public void RestoreScrollState(IPeekResultPresentation presentation) {}
  }

  internal class RotPeekResultSource : IPeekResultSource
  {
    private readonly string _relName, _expandedTactic, _activeTactic;

    public RotPeekResultSource(string relationshipName, string expandedTactic, string activeTactic)
    {
      _relName = relationshipName;
      _expandedTactic = expandedTactic;
      _activeTactic = activeTactic;
    }

    public void FindResults(string relationshipName, IPeekResultCollection resultCollection, 
      CancellationToken cancellationToken, IFindPeekResultsCallback callback)
    {
      if(relationshipName==_relName) resultCollection.Add(new RotPeekResult(_expandedTactic, _activeTactic));
    }
  }
}