using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace DafnyLanguage
{
    internal class EmbeddedTacticViewerFactory
    {
        public static void CreateAndEmbeddedTacticViewerToCollection(double w, double h, ref StackPanel c, IServiceProvider isp)
        {
            Contract.Requires(c!=null);
            var etv = new EmbeddedTacticViewer(w,h,isp);
            c.Children.Add(etv);
        }
    }

    internal class EmbeddedTacticViewer : ContentPresenter
    {
        private const string SampleTactic = @"H:\Dafny\tacny\examples\some_example_detritus.resolved_tactics";
        private readonly IServiceProvider _isp;
   
        public EmbeddedTacticViewer(double w, double h, IServiceProvider isp)
        {
            _isp = isp;
            Width = w - 10;
            Height = h - 10;
            Margin = new Thickness(5);
            Content = CreateEditor();
        }
        
        public IWpfTextViewHost CreateEditor()
        {
            var componentModel = (IComponentModel)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SComponentModel));
            var invisibleEditorManager = (IVsInvisibleEditorManager)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsInvisibleEditorManager));
            var editorAdapter = componentModel.GetService<IVsEditorAdaptersFactoryService>();

            IVsInvisibleEditor invisibleEditor;
            invisibleEditorManager.RegisterInvisibleEditor(SampleTactic, null, 
                (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING, null, out invisibleEditor);

            IntPtr docDataPointer;
            var guidIVsTextLines = typeof(IVsTextLines).GUID;
            invisibleEditor.GetDocData(1, ref guidIVsTextLines, out docDataPointer);
            var docData = (IVsTextLines)Marshal.GetObjectForIUnknown(docDataPointer);
            
            var codeWindow = editorAdapter.CreateVsCodeWindowAdapter(_isp);
            codeWindow.SetBuffer(docData);
            
            IVsTextView textView;
            codeWindow.GetPrimaryView(out textView);
            
            return editorAdapter.GetWpfTextViewHost(textView);
        }
    }
    
}
