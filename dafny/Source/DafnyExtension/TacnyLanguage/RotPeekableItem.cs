using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
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
  internal class RotPeekableItemSourceProvider : IPeekableItemSourceProvider
  {
    public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer) {
      return new RotPeekableItemSource(textBuffer);
    }
  }

  #endregion provider

  internal class RotPeekableItem : IPeekableItem
  {
    private readonly string _expandedTactic;

    public RotPeekableItem(string expandedTactic)
    {
      _expandedTactic = expandedTactic;
    }

    public IPeekResultSource GetOrCreateResultSource(string relationshipName) {
      return new RotPeekResultSource(relationshipName, _expandedTactic);
    }

    public string DisplayName => RotPeekRelationship.SDisplayName;

    public IEnumerable<IPeekRelationship> Relationships => 
      new IPeekRelationship[] { new RotPeekRelationship() };
  }

  internal class RotPeekableItemSource : IPeekableItemSource
  {
    private readonly ITextBuffer _textBuffer;
    
    private static IWpfTextView ActiveTextView {
      get {
        IVsTextView view = null;
        var textManager = (IVsTextManager) ServiceProvider.GlobalProvider.GetService(typeof(SVsTextManager));
        if (textManager?.GetActiveView(1, null, out view) != Microsoft.VisualStudio.VSConstants.S_OK) return null;
        var cm = (IComponentModel) ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel));
        var ea = cm?.GetService<IVsEditorAdaptersFactoryService>();
        var textView = ea?.GetWpfTextView(view);
        return textView;
      }
    }

    public RotPeekableItemSource(ITextBuffer textBuffer) {
      _textBuffer = textBuffer;
    }

    private string ExpandTactic()
    {
      var atv = ActiveTextView;
      if(atv==null) throw new NullReferenceException("Text View Not Accessible");
      var caret = atv.Caret.Position.BufferPosition.Position;
      return TacticReplacer.GetStringForRot(caret, _textBuffer);
    }

    public void Dispose() {}

    public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems) {
      if(session.RelationshipName == RotPeekRelationship.SName) peekableItems.Add(new RotPeekableItem(ExpandTactic()));
    }
  }
}