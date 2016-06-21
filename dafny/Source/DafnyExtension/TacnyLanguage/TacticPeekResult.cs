using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
    internal class TacticPeekResult : IPeekResult
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void NavigateTo(object data)
        {
            throw new NotImplementedException();
        }

        public IPeekResultDisplayInfo DisplayInfo { get; }
        public bool CanNavigateTo { get; }
        public Action<IPeekResult, object, object> PostNavigationCallback { get; }
        public event EventHandler Disposed;
    }

    [Export(typeof(IPeekResultPresenter))]
    [Name("TacticPeekResultPresenter")]
    internal class TacticPeekResultPresenter : IPeekResultPresenter
    {
        public IPeekResultPresentation TryCreatePeekResultPresentation(IPeekResult result)
        {
            throw new NotImplementedException();
        }
    }
    internal class TacticPeekResultPresentation : IPeekResultPresentation
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool TryOpen(IPeekResult otherResult)
        {
            throw new NotImplementedException();
        }

        public bool TryPrepareToClose()
        {
            throw new NotImplementedException();
        }

        public UIElement Create(IPeekSession session, IPeekResultScrollState scrollState)
        {
            throw new NotImplementedException();
        }

        public void ScrollIntoView(IPeekResultScrollState scrollState)
        {
            throw new NotImplementedException();
        }

        public IPeekResultScrollState CaptureScrollState()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void SetKeyboardFocus()
        {
            throw new NotImplementedException();
        }

        public bool CanSave(out string defaultPath)
        {
            throw new NotImplementedException();
        }

        public bool TrySave(bool saveAs)
        {
            throw new NotImplementedException();
        }

        public double ZoomLevel { get; set; }
        public bool IsDirty { get; }
        public bool IsReadOnly { get; }
        public event EventHandler<RecreateContentEventArgs> RecreateContent;
        public event EventHandler IsDirtyChanged;
        public event EventHandler IsReadOnlyChanged;
    }
    internal class TacticPeekResultSource : IPeekResultSource
    {
        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken,
            IFindPeekResultsCallback callback)
        {
            throw new NotImplementedException();
        }
    }
}
