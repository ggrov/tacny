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

    public IPeekResultDisplayInfo DisplayInfo => new PeekResultDisplayInfo(Title, Tip, Title, Tip);
    
    public void Dispose() {}
    public void NavigateTo(object data) {}
    public bool CanNavigateTo => false;
    public Action<IPeekResult, object, object> PostNavigationCallback => (pr, o, data) => { };
    public event EventHandler Disposed { add{} remove{} }
  }

  internal class RotPeekResultPresentation : IPeekResultPresentation, IDesiredHeightProvider
  {
    private readonly string _expandedTactic;
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

    public bool TryOpen(IPeekResult otherResult)
    {
      var other = otherResult as RotPeekResult;
      return _expandedTactic==other?.ExpandedTactic;
    } 
    
    public bool CanSave(out string defaultPath) {
      defaultPath = null;
      return false;
    }

    public IPeekResultScrollState CaptureScrollState() => new RotPeekResultScrollState();
    public void SetKeyboardFocus() => _tb.Focus();
    
    public void Close() {}
    public void Dispose() {}
    public void ScrollIntoView(IPeekResultScrollState scrollState) {}

    public bool TrySave(bool saveAs) => true;
    public bool TryPrepareToClose() => true;
    public bool IsDirty => false;
    public bool IsReadOnly => true;

    public double ZoomLevel { get; set; }
    public event EventHandler<RecreateContentEventArgs> RecreateContent {add{} remove{}}
    public event EventHandler IsDirtyChanged {add{} remove{}}
    public event EventHandler IsReadOnlyChanged {add{} remove{}}
    public event EventHandler<EventArgs> DesiredHeightChanged {add{} remove{}}
  }

  internal class RotPeekResultScrollState : IPeekResultScrollState
  {
    public void Dispose() {}
    public void RestoreScrollState(IPeekResultPresentation presentation) {}
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