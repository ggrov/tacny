﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using DafnyLanguage.DafnyMenu;
using Microsoft.Dafny;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace DafnyLanguage.TacnyLanguage
{
  [Export(typeof(IVsTextViewCreationListener))]
  [ContentType("dafny")]
  [Name("TacticReplacerProvider")]
  [TextViewRole(PredefinedTextViewRoles.Editable)]
  internal class TacticReplacerProvider : IVsTextViewCreationListener
  {
    [Import(typeof(ITextDocumentFactoryService))]
    internal ITextDocumentFactoryService Tdf { get; set; }
    
    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider ServiceProvider { get; set; }

    [Import]
    internal IPeekBroker Pb { get; set; }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
      var vsShell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
      if (vsShell == null) throw new NullReferenceException("VS Shell failed to Load");
      IVsPackage shellPack;
      var packToLoad = new Guid("e1baf989-88a6-4acf-8d97-e0dc243476aa");
      if (vsShell.LoadPackage(ref packToLoad, out shellPack) != VSConstants.S_OK)
        throw new NullReferenceException("Dafny Menu failed to Load");
      var dafnyMenuPack = (DafnyMenuPackage)shellPack;
      dafnyMenuPack.TacnyMenuProxy = new TacticReplacerProxy(Tdf, ServiceProvider, Pb);
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

    public TacticReplacerProxy(ITextDocumentFactoryService tdf, IServiceProvider isp, IPeekBroker pb) {
      var status = (IVsStatusbar) isp.GetService(typeof(SVsStatusbar));
      _tr = new TacticReplacer(status, tdf, pb);
    }
    
    public void ReplaceOne(IWpfTextView atv)
    {
      Contract.Assume(atv!=null);
      _tr.ReplaceMethodUnderCaret(atv);
    }

    public void ShowRot(IWpfTextView atv)
    {
      Contract.Assume(atv != null);
      _tr.ShowRotPeekForMethod(atv);
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
    private static IPeekBroker _pb;

    public TacticReplacer(IVsStatusbar sb, ITextDocumentFactoryService tdf, IPeekBroker pb) {
      _status = sb;
      _tdf = tdf;
      _pb = pb;
    }
    
    public TacticReplaceStatus ReplaceAll(ITextBuffer tb) {
      string file;
      Program program;
      if (!LoadAndCheckDocument(tb, out file))
        return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      var tld = LoadAndResolveTld(tb, file, out program);
      var unresolvedProgram = GetUnresolvedProgram(tb, file);

      var tedit = tb.CreateEdit();
      var status = TacticReplaceStatus.NoTactic;
      foreach (var member in tld.Members)
      {
        if (!member.CallsTactic) continue;
        status = ReplaceMember(member, program, unresolvedProgram, tedit);
        if (status != TacticReplaceStatus.Success) break;
      }
      if (status == TacticReplaceStatus.Success) {
        tedit.Apply();
      } else {
        tedit.Dispose();
      }
      return NotifyOfReplacement(status);
    }

    public TacticReplaceStatus ShowRotPeekForMethod(IWpfTextView tv) {
      Program program;
      MemberDecl member;
      string filePath, expandedString;
      
      if (!LoadAndCheckDocument(tv.TextBuffer, out filePath)) return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      var caretPos = tv.Caret.Position.BufferPosition.Position;
      var resolveStatus = LoadAndResolveMemberAtPosition(caretPos, filePath, tv.TextBuffer, out program, out member);
      var unresolvedProgram = GetUnresolvedProgram(tv.TextBuffer, filePath);
      if (resolveStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(resolveStatus);
      if (!member.CallsTactic) return NotifyOfReplacement(TacticReplaceStatus.NoTactic);
      var expandStatus = ExpandTactic(program, member, unresolvedProgram, out expandedString);
      if (expandStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(expandStatus);

      var trigger = tv.TextBuffer.CurrentSnapshot.CreateTrackingPoint(member.BodyStartTok.pos-1, PointTrackingMode.Positive);
      _pb.TriggerPeekSession(tv, trigger, RotPeekRelationship.SName);
      return NotifyOfReplacement(TacticReplaceStatus.Success);
    }

    private static Program GetUnresolvedProgram(ITextBuffer tb, string filename) {
      var driver = new TacnyDriver(tb, filename);
      return driver.ParseAndTypeCheck(false);
    }

    public static string GetStringForRot(int position, ITextBuffer tb)
    {
      var methodName = new SnapshotSpan();
      var fullMethod = GetExpandedTactic(position, tb, ref methodName);
      var splitMethod = fullMethod.Split('\n');
      return splitMethod.Where((t, i) => i >= 2 && i <= splitMethod.Length - 3).Aggregate("", (current, t) => current + t + "\n");
    }

    public TacticReplaceStatus ReplaceMethodUnderCaret(IWpfTextView tv) {
      Program program;
      MemberDecl member;
      string filePath;

      if (!LoadAndCheckDocument(tv.TextBuffer, out filePath)) return NotifyOfReplacement(TacticReplaceStatus.NoDocumentPersistence);
      var caretPos = tv.Caret.Position.BufferPosition.Position;
      var resolveStatus = LoadAndResolveMemberAtPosition(caretPos, filePath, tv.TextBuffer, out program, out member);
      var unresolvedProgram = GetUnresolvedProgram(tv.TextBuffer, filePath);
      if (resolveStatus != TacticReplaceStatus.Success) return NotifyOfReplacement(resolveStatus);

      var tedit = tv.TextBuffer.CreateEdit();
      var status = ReplaceMember(member, program, unresolvedProgram, tedit);
      if (status == TacticReplaceStatus.Success) tedit.Apply();
      return NotifyOfReplacement(status);
    }

    public static string GetExpandedTactic(int position, ITextBuffer buffer, ref SnapshotSpan methodName)
    {
      Program program;
      MemberDecl member;
      string file;

      if (!LoadAndCheckDocument(buffer, out file)) return null;
      var resolveStatus = LoadAndResolveMemberAtPosition(position, file, buffer, out program, out member);
      var unresolvedProgram = GetUnresolvedProgram(buffer, file);
      if (resolveStatus != TacticReplaceStatus.Success) return null;
      methodName = new SnapshotSpan(buffer.CurrentSnapshot, member.tok.pos, member.CompileName.Length);

      string expanded;
      ExpandTactic(program, member, unresolvedProgram, out expanded);
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

    private static TacticReplaceStatus ReplaceMember(MemberDecl member, Program program, Program unresolvedProgram, ITextEdit tedit) {
      var startOfBlock = member.tok.pos;
      var lengthOfBlock = member.BodyEndTok.pos - startOfBlock + 1;

      string expandedTactic;
      var expandedStatus = ExpandTactic(program, member, unresolvedProgram, out expandedTactic);
      if (expandedStatus != TacticReplaceStatus.Success)
        return expandedStatus;

      tedit.Replace(startOfBlock, lengthOfBlock, expandedTactic);
      return TacticReplaceStatus.Success;
    }
    
    private static TacticReplaceStatus LoadAndResolveMemberAtPosition(int position, string file, ITextBuffer tb, out Program program, out MemberDecl member)
    {
      member = null;
      var tld = LoadAndResolveTld(tb, file, out program);
      if (tld == null) return TacticReplaceStatus.NotResolved;
      
      member = (from m in tld.Members
                where m.tok.pos <= position && position <= m.BodyEndTok.pos+1
                select m).FirstOrDefault();
      return member==null ? TacticReplaceStatus.NoTactic : TacticReplaceStatus.Success;
    }

    private static DefaultClassDecl LoadAndResolveTld(ITextBuffer tb, string file, out Program program) {
      var driver = new TacnyDriver(tb, file);
      program = driver.ParseAndTypeCheck(true);
      return (DefaultClassDecl)program?.DefaultModuleDef.TopLevelDecls.FirstOrDefault();
    }

    private static TacticReplaceStatus ExpandTactic(Program program, MemberDecl member, Program unresolvedProgram, out string expandedTactic)
    {
      expandedTactic = null;
      if (!member.CallsTactic) return TacticReplaceStatus.NoTactic;

      var status = TacticReplaceStatus.Success;
      var evaluatedMember = Tacny.Interpreter.FindAndApplyTactic(program, member, errorInfo => {status = TacticReplaceStatus.TranslatorFail;}, unresolvedProgram);
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