using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DafnyLanguage.DafnyMenu;
using DafnyLanguage.TacnyLanguage;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace DafnyLanguage
{
  class MenuProxy : DafnyLanguage.DafnyMenu.IMenuProxy
  {
    private DafnyMenu.DafnyMenuPackage DafnyMenuPackage;

    public MenuProxy(DafnyMenu.DafnyMenuPackage DafnyMenuPackage)
    {
      this.DafnyMenuPackage = DafnyMenuPackage;
    }

    public int ToggleSnapshotVerification(IWpfTextView activeTextView)
    {
      return DafnyDriver.ChangeIncrementalVerification(1);
    }

    public int ToggleMoreAdvancedSnapshotVerification(IWpfTextView activeTextView)
    {
      return DafnyDriver.ChangeIncrementalVerification(2);
    }

    public bool MoreAdvancedSnapshotVerificationCommandEnabled(IWpfTextView activeTextView)
    {
      return activeTextView != null
             && 0 < DafnyDriver.IncrementalVerificationMode();
    }

    public bool ToggleAutomaticInduction(IWpfTextView activeTextView) {
      return DafnyDriver.ChangeAutomaticInduction();
    }

    public bool AutomaticInductionCommandEnabled(IWpfTextView activeTextView) {
      return activeTextView != null;
    }

    public bool StopVerifierCommandEnabled(IWpfTextView activeTextView)
    {
      DafnyLanguage.ProgressTagger tagger;
      return activeTextView != null
                    && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(activeTextView.TextBuffer, out tagger)
                    && tagger != null && tagger.VerificationDisabled;
    }

    public void StopVerifier(IWpfTextView activeTextView)
    {
      DafnyLanguage.ProgressTagger tagger;
      if (activeTextView != null && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(activeTextView.TextBuffer, out tagger))
      {
        tagger.StopVerification();
      }
    }

    public bool RunVerifierCommandEnabled(IWpfTextView activeTextView)
    {
      DafnyLanguage.ProgressTagger tagger;
      return activeTextView != null
                 && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(activeTextView.TextBuffer, out tagger)
                 && tagger != null && tagger.VerificationDisabled;
    }

    public void RunVerifier(IWpfTextView activeTextView)
    {
      DafnyLanguage.ProgressTagger tagger;
      if (activeTextView != null && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(activeTextView.TextBuffer, out tagger))
      {
        tagger.StartVerification();
      }
    }

    public void DiagnoseTimeouts(IWpfTextView activeTextView)
    {
      DafnyLanguage.ProgressTagger tagger;
      if (activeTextView != null && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(activeTextView.TextBuffer, out tagger))
      {
        tagger.StartVerification(false, true);
      }
    }

    public bool MenuEnabled(IWpfTextView activeTextView)
    {
      return activeTextView != null && activeTextView.TextBuffer.ContentType.DisplayName == "dafny";
    }

    public bool CompileCommandEnabled(IWpfTextView activeTextView)
    {
      ResolverTagger resolver;
      return activeTextView != null
                    && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(activeTextView.TextBuffer, out resolver)
                    && resolver.Program != null;
    }

    public bool DiagnoseTimeoutsCommandEnabled(IWpfTextView activeTextView)
    {
      ResolverTagger resolver;
      return activeTextView != null
                    && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(activeTextView.TextBuffer, out resolver)
                    && resolver.VerificationErrors.Any(err => err.Message.Contains("timed out"));
    }

    public void Compile(IWpfTextView activeTextView)
    {
      ResolverTagger resolver;
      if (activeTextView != null
          && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(activeTextView.TextBuffer, out resolver)
          && resolver.Program != null)
      {
        var outputWriter = new StringWriter();
        DafnyMenuPackage.ExecuteAsCompiling(() => { DafnyDriver.Compile(resolver.Program, outputWriter); }, outputWriter);
      }
    }

    public bool ShowErrorModelCommandEnabled(IWpfTextView activeTextView)
    {
      ResolverTagger resolver;
      return activeTextView != null
                    && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(activeTextView.TextBuffer, out resolver)
                    && resolver.Program != null
                    && resolver.VerificationErrors.Any(err => !string.IsNullOrEmpty(err.ModelText));
    }

    public void ShowErrorModel(IWpfTextView activeTextView)
    {
      ResolverTagger resolver = null;
      var show = activeTextView != null
                 && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(activeTextView.TextBuffer, out resolver)
                 && resolver.Program != null
                 && resolver.VerificationErrors.Any(err => err.IsSelected && !string.IsNullOrEmpty(err.ModelText));
      if (show)
      {
        var selectedError = resolver.VerificationErrors.FirstOrDefault(err => err.IsSelected && !string.IsNullOrEmpty(err.ModelText));

        if (selectedError != null)
        {
          DafnyMenuPackage.ShowErrorModelInBVD(selectedError.ModelText, selectedError.SelectedStateId);
        }
      }
    }

    public bool ToggleTacticEvaluation()
    {
      return DafnyDriver.ToggleTacticEvaluation();
    }
  }
}
