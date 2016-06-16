using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace DafnyLanguage
{
    #region provider

    [Export(typeof(IGlyphFactoryProvider))]
    [Name("TacticGlyph")]
    [Order(After = "VsTextMarker")]
    [ContentType("dafny")]
    [TagType(typeof(TacticTag))]
    internal sealed class TacticGlyphFactoryProvider : IGlyphFactoryProvider
    {
        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
        {
            return new TacticGlyphFactory(margin);
        }
    }


    [Export(typeof(ITaggerProvider))]
    [ContentType("dafny")]
    [TagType(typeof(TacticTag))]
    class TacticTaggerProvider : ITaggerProvider
    {
        [Import] internal IClassifierAggregatorService AggregatorService;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            return new TacticTagger(AggregatorService.GetClassifier(buffer)) as ITagger<T>;
        }
    }


    [Export(typeof(IGlyphMouseProcessorProvider))]
    [ContentType("dafny")]
    [Order]
    [Name("TacticGlyphMouseProcessorProvider")]
    internal class TacticGlyphMouseProcessorProvider : IGlyphMouseProcessorProvider
    {
        [Import(typeof(SVsServiceProvider))] IServiceProvider isp;
        public IMouseProcessor GetAssociatedMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin margin)
        {
            return new TacticGlyphMouseProcessor(wpfTextViewHost, isp);
        }
    }


    #endregion

    #region tagger

    internal class TacticGlyphMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextViewHost _tvh;
        private readonly IServiceProvider _isp;

        public TacticGlyphMouseProcessor(IWpfTextViewHost tvh, IServiceProvider isp)
        {
            _tvh = tvh;
            _isp = isp;
        }

        public override void PostprocessMouseDown(MouseButtonEventArgs e)
        {
            var tv = _tvh.TextView;
            var position = e.GetPosition(tv.VisualElement);
            var line = tv.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y);
            var lineContents = line.Extent.GetText();

            VsShellUtilities.ShowMessageBox(_isp, lineContents, "Picked Line:", 
                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

        }
    }

    internal class TacticTag : IGlyphTag { }

    internal class TacticGlyphFactory : IGlyphFactory
    {
        public TacticGlyphFactory(IWpfTextViewMargin margin)
        {
        }

        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            if (!(tag is TacticTag))
            {
                return null;
            }

            System.Windows.Shapes.Ellipse ellipse = new Ellipse
            {
                Fill = Brushes.DarkCyan,
                Height = 8,
                Width = 8,
                ToolTip = "Show Tactic"
            };
            
            return ellipse;
        }
    }



    internal class TacticTagger : ITagger<TacticTag>
    {
        private readonly IClassifier _classifier;

        public TacticTagger(IClassifier classifier)
        {
            _classifier = classifier;
        }

        public IEnumerable<ITagSpan<TacticTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan span in spans)
            {
                foreach (ClassificationSpan classification in _classifier.GetClassificationSpans(span))
                {
                    if (classification.ClassificationType.Classification.Contains("Dafny identifier"))
                    {
                        yield return new TagSpan<TacticTag>(classification.Span, new TacticTag());
                    }
                    
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }

    #endregion

}
