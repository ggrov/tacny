using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Tacny;

namespace DafnyLanguage.TacnyLanguage
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("dafny")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class TacticReplacerFilterProvider : IVsTextViewCreationListener
  {
    [Import(typeof(ITextDocumentFactoryService))]
    internal ITextDocumentFactoryService Tdf { get; set; }
    
    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider { get; set; }
    
    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
      if (vsShell == null) throw new NullReferenceException("VS Shell failed to Load");
      IVsPackage shellPack;
      var packToLoad = new Guid("e1baf989-88a6-4acf-8d97-e0dc243476aa");
      if (vsShell.LoadPackage(ref packToLoad, out shellPack) != VSConstants.S_OK)
        throw new NullReferenceException("Dafny Menu failed to Load");
      var dafnyMenuPack = (DafnyMenuPackage)shellPack;
      dafnyMenuPack.TacnyMenuProxy = new TacticReplacerProxy(Tdf, ServiceProvider);
    }
  }

  public class TacticReplacerProxy : ITacnyMenuProxy
  {
    private readonly ITextDocumentFactoryService _tdf;
    private readonly IVsStatusbar _status;

    public TacticReplacerProxy(ITextDocumentFactoryService tdf, IServiceProvider isp) {
      _tdf = tdf;
      _status = (IVsStatusbar) isp.GetService(typeof(SVsStatusbar));
    }
    
    public void Exec(IWpfTextView atv)
    {
      Contract.Assume(atv!=null);
      var trcf = new TacticReplacerCommandFilter(atv, _status, _tdf);
      trcf.Exec();
    }
  }

  public class TacticReplacerCommandFilter
  {
    private readonly IWpfTextView _tv;
    private readonly ITextDocumentFactoryService _tdf;
    private readonly IVsStatusbar _status;
    private ITextDocument _document;

    public TacticReplacerCommandFilter(IWpfTextView textView, IVsStatusbar sb, ITextDocumentFactoryService tdf)
    {
      _tv = textView;
      _tdf = tdf;
      LoadAndCheckDocument();
      _status = sb;
    }

    public void Exec()
    {
      var status = ExecuteReplacement();
      NotifyOfReplacement(status);
    }
    
    private void NotifyOfReplacement(TacticReplaceStatus t)
    {
      switch (t)
      {
        case TacticReplaceStatus.NoDocumentPersistence:
          _status.SetText("Document must be saved in order to expand tactics.");
          break;
        case TacticReplaceStatus.TranslatorFail:
          _status.SetText("Tacny was unable to expand requested tactics.");
          break;
        case TacticReplaceStatus.NotResolved:
          _status.SetText("File must first be resolvable to expand tactics.");
          break;
        case TacticReplaceStatus.NoTactic:
          _status.SetText("There is no method under the caret that has expandable tactics.");
          break;
        case TacticReplaceStatus.Success:
          _status.SetText("Tactic expanded succesfully.");
          break;
        default:
          throw new Exception("Escaped Switch with "+t);
      }
    }

    internal enum TacticReplaceStatus
    {
      Success,
      NoDocumentPersistence,
      NoTactic,
      NotResolved,
      TranslatorFail
    }
    
    private bool LoadAndCheckDocument()
    {
      _tdf.TryGetTextDocument(_tv.TextBuffer, out _document);
      return !string.IsNullOrEmpty(_document?.FilePath);
    }
    
    private TacticReplaceStatus ExecuteReplacement()
    {
      Microsoft.Dafny.Program program;
      MemberDecl member;
      
      var resolveStatus = LoadResolveForCaretPosition(out program, out member);
      if (resolveStatus != TacticReplaceStatus.Success) return resolveStatus;

      var startOfBlock = member.tok.pos;
      var lengthOfBlock = member.BodyEndTok.pos - startOfBlock + 1;

      string expandedTactic;
      var expandedStatus = GetExpandedTactic(program, member, out expandedTactic);
      if (expandedStatus != TacticReplaceStatus.Success)
        return expandedStatus;

      var tedit = _tv.TextBuffer.CreateEdit();
      tedit.Replace(startOfBlock, lengthOfBlock, expandedTactic);
      tedit.Apply();
      return TacticReplaceStatus.Success;
    }

    private TacticReplaceStatus LoadResolveForCaretPosition(out Microsoft.Dafny.Program program, out MemberDecl member)
    {
      program = null;
      member = null;
      if (!LoadAndCheckDocument()) return TacticReplaceStatus.NoDocumentPersistence;
      var caretPos = _tv.Caret.Position.BufferPosition.Position;

      var driver = new DafnyDriver(_tv.TextBuffer, _document.FilePath);
      program = driver.ProcessResolution(true, true);
      var tld = (DefaultClassDecl)program?.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
      if (tld == null) return TacticReplaceStatus.NotResolved;
      
      member = (from m in tld.Members
                          where m.tok.pos <= caretPos && caretPos <= m.BodyEndTok.pos+1
                          select m).FirstOrDefault();
      return member==null ? TacticReplaceStatus.NoTactic : TacticReplaceStatus.Success;
    }
    
    private static TacticReplaceStatus GetExpandedTactic(Microsoft.Dafny.Program program, MemberDecl member, out string expandedTactic)
    {
      expandedTactic = "";
      if (!member.CallsTactic) return TacticReplaceStatus.NoTactic;
      
      var status = TacticReplaceStatus.Success;
      var evaluatedMember = Interpreter.FindAndApplyTactic(program, member, errorInfo => {status = TacticReplaceStatus.TranslatorFail;});
      if (evaluatedMember == null || status != TacticReplaceStatus.Success) return TacticReplaceStatus.TranslatorFail;

      var sr = new StringWriter();
      var printer = new Printer(sr);
      printer.PrintMembers(new List<MemberDecl> { evaluatedMember }, 0, program.FullName);
      expandedTactic = StripExtraContentFromExpanded(sr.ToString());
      return TacticReplaceStatus.Success;
    }

    private static string StripExtraContentFromExpanded(string expandedTactic) {
      var words = new [] {"ghost ", "lemma ", "method ", "function ", "tactic "};
      return words.Aggregate(expandedTactic, RazeFringe);
    }

    private static string RazeFringe(string head, string fringe)
    {
      return head.Substring(0, fringe.Length)==fringe ? head.Substring(fringe.Length) : head;
    }
  }
}