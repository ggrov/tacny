using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.Refactoring
{

  #region provider
  internal sealed class RotPeekRelationship : IPeekRelationship
  {
    public static string SName => "RotPeek";
    public static string SDisplayName => "Read Only Tactic Peek";

    public string Name => SName;
    public string DisplayName => SDisplayName;
  }

  [Export(typeof(IPeekableItemSourceProvider))]
  [ContentType("dafny")]
  [Name("RotPeekableItemSourceProvider")]
  [SupportsStandaloneFiles(true)]
  internal class RotPeekableItemSourceProvider : IPeekableItemSourceProvider
  {
    public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer) {
      return new RotPeekableItemSource();
    }
  }

  #endregion provider

  internal class RotPeekableItem : IPeekableItem
  {
    private readonly string _expandedTactic;
    private readonly string _activeTactic;

    public RotPeekableItem(Tuple<string, string> expandedTactic)
    {
      _expandedTactic = expandedTactic.Item1;
      _activeTactic = expandedTactic.Item2;
    }

    public IPeekResultSource GetOrCreateResultSource(string relationshipName) {
      return new RotPeekResultSource(relationshipName, _expandedTactic, _activeTactic);
    }

    public string DisplayName => RotPeekRelationship.SDisplayName;

    public IEnumerable<IPeekRelationship> Relationships => 
      new IPeekRelationship[] { new RotPeekRelationship() };
  }

  internal class RotPeekableItemSource : IPeekableItemSource
  {
    public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems)
    {
      if (session.RelationshipName != RotPeekRelationship.SName) return;
      var s = DafnyClassifier.DafnyMenuPackage.TacnyMenuProxy.GetExpandedForPeekSession(session);
      if (s == null) return;
      peekableItems.Add(new RotPeekableItem(s));
    }

    public void Dispose() {}
  }
}