using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType("dafny")]
    [Name("TacticPeekableItemSourceProvider")]
    internal class TacticPeekableItemSourceProvider : IPeekableItemSourceProvider
    {
        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }
    }

    internal class TacticPeekableItem : IPeekableItem
    {
        public IPeekResultSource GetOrCreateResultSource(string relationshipName)
        {
            throw new NotImplementedException();
        }

        public string DisplayName { get; }
        public IEnumerable<IPeekRelationship> Relationships { get; }
    }

    internal class TacticPeekableItemSource : IPeekableItemSource
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems)
        {
            throw new NotImplementedException();
        }
    }

}
