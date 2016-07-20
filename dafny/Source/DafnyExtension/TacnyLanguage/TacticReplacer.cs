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

  public enum TacticReplaceStatus
  {
    Success,
    NoDocumentPersistence,
    NoTactic,
    NotResolved,
    TranslatorFail
  }

  public class TacticReplacerProxy : ITacnyMenuProxy
  {
    private readonly TacticReplacer _tr;

    public TacticReplacerProxy(ITextDocumentFactoryService tdf, IServiceProvider isp) {
      var status = (IVsStatusbar) isp.GetService(typeof(SVsStatusbar));
      _tr = new TacticReplacer(status, tdf);
    }
    
    public void ReplaceOne(IWpfTextView atv)
    {
      Contract.Assume(atv!=null);
      _tr.ReplaceMethodUnderCaret(atv);
    }

    public void ReplaceAll(ITextBuffer tb)
    {
      Contract.Assume(tb!=null);
      _tr.ReplaceAll(tb);
    }
  }

  public class TacticReplacer
  {
    private static IVsStatusbar _status;
    private static ITextDocumentFactoryService _tdf;

    public TacticReplacer(IVsStatusbar sb, ITextDocumentFactoryService tdf) {
      _status = sb;
      _tdf = tdf;
    }
    
    public TacticReplaceStatus ReplaceAll(ITextBuffer tb) {
      string file;
      Microsoft.Dafny.Program program;
      if (!LoadAndCheckDocument(tb, out file))
        return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      var tld = LoadAndResolveTld(tb, file, out program);

      var tedit = tb.CreateEdit();
      var status = TacticReplaceStatus.NoTactic;
      foreach (var member in tld.Members)
      {
        if (!member.CallsTactic) continue;
        status = ReplaceMember(member, program, tedit);
        if (status != TacticReplaceStatus.Success) break;
      }
      if (status == TacticReplaceStatus.Success) {
        tedit.Apply();
      } else {
        tedit.Dispose();
      }
      return NotifyOfReplacement(status);
    }

    public TacticReplaceStatus ReplaceMethodUnderCaret(IWpfTextView tv)
    {
      Microsoft.Dafny.Program program;
      MemberDecl member;
      string filePath;

      if (!LoadAndCheckDocument(tv.TextBuffer, out filePath)) return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      var caretPos = tv.Caret.Position.BufferPosition.Position;
      var resolveStatus = LoadAndResolveMemberAtPosition(caretPos, filePath, tv.TextBuffer, out program, out member);
      if (resolveStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(resolveStatus);

      var tedit = tv.TextBuffer.CreateEdit();
      var status = ReplaceMember(member, program, tedit);
      if (status == TacticReplaceStatus.Success) tedit.Apply();
      return NotifyOfReplacement(status);
    }

    public static string GetExpandedTactic(int position, ITextBuffer buffer, ref SnapshotSpan methodName)
    {
      Microsoft.Dafny.Program program;
      MemberDecl member;
      string file;
      
      if (!LoadAndCheckDocument(buffer, out file)) return null;
      var resolveStatus = LoadAndResolveMemberAtPosition(position, file, buffer, out program, out member);
      if (resolveStatus != TacticReplaceStatus.Success) return null;
      methodName = new SnapshotSpan(buffer.CurrentSnapshot, member.tok.pos, member.CompileName.Length);

      string expanded;
      ExpandTactic(program, member, out expanded);
      return expanded;
    }

    private static TacticReplaceStatus NotifyOfReplacement(TacticReplaceStatus t)
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
      return t;
    }

    private static bool LoadAndCheckDocument(ITextBuffer tb, out string filePath) {
      ITextDocument doc;
      _tdf.TryGetTextDocument(tb, out doc);
      filePath = doc?.FilePath;
      return !string.IsNullOrEmpty(filePath);
    }

    private static TacticReplaceStatus ReplaceMember(MemberDecl member, Microsoft.Dafny.Program program, ITextEdit tedit) {
      var startOfBlock = member.tok.pos;
      var lengthOfBlock = member.BodyEndTok.pos - startOfBlock + 1;

      string expandedTactic;
      var expandedStatus = ExpandTactic(program, member, out expandedTactic);
      if (expandedStatus != TacticReplaceStatus.Success)
        return expandedStatus;

      tedit.Replace(startOfBlock, lengthOfBlock, expandedTactic);
      return TacticReplaceStatus.Success;
    }
    
    private static TacticReplaceStatus LoadAndResolveMemberAtPosition(int position, string file, ITextBuffer tb, out Microsoft.Dafny.Program program, out MemberDecl member)
    {
      member = null;
      var tld = LoadAndResolveTld(tb, file, out program);
      if (tld == null) return TacticReplaceStatus.NotResolved;
      
      member = (from m in tld.Members
                where m.tok.pos <= position && position <= m.BodyEndTok.pos+1
                select m).FirstOrDefault();
      return member==null ? TacticReplaceStatus.NoTactic : TacticReplaceStatus.Success;
    }

    private static DefaultClassDecl LoadAndResolveTld(ITextBuffer tb, string file, out Microsoft.Dafny.Program program) {
      var driver = new DafnyDriver(tb, file);
      program = driver.ProcessResolution(true, true);
      return (DefaultClassDecl)program?.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
    }

    private static TacticReplaceStatus ExpandTactic(Microsoft.Dafny.Program program, MemberDecl member, out string expandedTactic)
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

    private static string RazeFringe(string body, string fringe)
    {
      return body.Substring(0, fringe.Length)==fringe ? body.Substring(fringe.Length) : body;
    }
  }
}