using System.Collections.Generic;
using System.ComponentModel.Composition;
using DafnyLanguage.TacnyLanguage;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
  //Peekable* is the data representation

  #region provider
 //TODO copy all comment notes into docs
  internal sealed class RotPeekRelationship : IPeekRelationship //Defines name of peek
  {
    public static string SName => "RotPeek";
    public static string SDisplayName => "Read Only Tactic Peek";

    public string Name => SName;
    public string DisplayName => SDisplayName;
  }

  [Export(typeof(IPeekableItemSourceProvider))]
  [ContentType("dafny")]
  [Name("RotPeekableItemSourceProvider")]
  internal class RotPeekableItemSourceProvider : IPeekableItemSourceProvider //This initializes / activates / gets the peek item source
  {
    public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer) {
      return new RotPeekableItemSource(textBuffer);
    }
  }

  #endregion provider

  internal class RotPeekableItem : IPeekableItem
    //an object that represents something peekable. This object can be a source of a PeekSession
  {
    public IPeekResultSource GetOrCreateResultSource(string relationshipName) {
      return new RotPeekResultSource(relationshipName);
    }

    public string DisplayName => RotPeekRelationship.SDisplayName;

    public IEnumerable<IPeekRelationship> Relationships => 
      new IPeekRelationship[] { new RotPeekRelationship() };
  }

  internal class RotPeekableItemSource : IPeekableItemSource //for a content type, provides PeekableItems
  {
    private readonly ITextBuffer _textBuffer;

    public RotPeekableItemSource(ITextBuffer textBuffer) {
      _textBuffer = textBuffer;
    }

    public void Dispose() {}

    public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems) {
      if(session.RelationshipName == RotPeekRelationship.SName) peekableItems.Add(new RotPeekableItem());
    }
  }
}