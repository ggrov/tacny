using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace DafnyLanguage.TacnyLanguage
{
    internal class TacticReplacer
    {
        private readonly IWpfTextView _tv;

        public TacticReplacer(IWpfTextView tv)
        {
            _tv = tv;
        }

        public void Execute()
        {
            var caretPos = _tv.Caret.Position;
            var selected = _tv.Selection;

            //var newSpan = DafnyDriver.makeSomeCallFor(chosenAreaToExpand);
            var newSpan = selected + "\n   expanded();\n   //stuff would\n   go = here++;\n";

            var startOfOldSpan = caretPos.BufferPosition.Position;
            var endOfOldSpan = startOfOldSpan + 1;
            Contract.Requires(startOfOldSpan < endOfOldSpan);

            var tedit = _tv.TextBuffer.CreateEdit();
            tedit.Delete(startOfOldSpan, endOfOldSpan);
            tedit.Insert(startOfOldSpan, newSpan);

            //want to remove the code that the tactic is replacing
            //i.e. the entire section of code from signature to closing brace
            //then insert to the position of the start of the sig, the newly rendered one, with tactic expanded
            tedit.Apply();
        }
    }
}
