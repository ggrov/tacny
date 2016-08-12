using System;
using System.Collections.Generic;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.VisualStudio.Text;
using Errors = Microsoft.Dafny.Errors;
using Parser = Microsoft.Dafny.Parser;
using Program = Microsoft.Dafny.Program;

namespace DafnyLanguage.Refactoring
{
  internal class TacnyDriver : DafnyDriver
  {
    public TacnyDriver(ITextBuffer buffer, string filename) : base(buffer, filename){}
    
    private static bool _tacticEvaluationIsEnabled = true;

    public static bool ToggleTacticEvaluation()
    {
      _tacticEvaluationIsEnabled = !_tacticEvaluationIsEnabled;
      Translator.TacticEvaluationIsEnabled = _tacticEvaluationIsEnabled;
      return _tacticEvaluationIsEnabled;
    }
    
    public Program ReParse(bool runResolver)
    {
      var errorReporter = new ConsoleErrorReporter();
      var module = new LiteralModuleDecl(new DefaultModuleDecl(), null);
      var builtIns = new BuiltIns();
      var parseErrors = new Errors(errorReporter);
      var errorCount = Parser.Parse(_snapshot.GetText(), _filename, _filename, module, builtIns, parseErrors);
      var errString = Main.ParseIncludes(module, builtIns, new List<string>(), parseErrors);
      if (errorCount != 0 || errString != null) return null;

      var program = new Program(_filename, module, builtIns, errorReporter);
      if (!runResolver) return program;

      var r = new Resolver(program);
      r.ResolveProgram(program);
      return errorReporter.Count(ErrorLevel.Error) == 0 ? program : null;
    }

    public bool GetExistingProgramFromBuffer(out Program program) {
      program = null;
      Tuple<ITextSnapshot, Program, List<DafnyError>> parseResult;
      if (!_buffer.Properties.TryGetProperty(bufferDafnyKey, out parseResult) || (parseResult.Item1 != _snapshot))
        return false;
      program = parseResult.Item2;
      return program!=null;
    }

    public static bool Verify(Program dafnyProgram, ResolverTagger resolver, string uniqueIdPrefix, string requestId, ErrorReporterDelegate er, Program unresolvedProgram)
    {
      var translator = new Translator(dafnyProgram.reporter, er)
      {
        InsertChecksums = true,
        UniqueIdPrefix = uniqueIdPrefix
      };
      var boogieProgram = translator.Translate(dafnyProgram, unresolvedProgram);
      resolver.ReInitializeVerificationErrors(requestId, boogieProgram.Implementations);
      var outcome = BoogiePipeline(boogieProgram, 1 < CommandLineOptions.Clo.VerifySnapshots ? uniqueIdPrefix : null, requestId, er);
      return outcome == PipelineOutcome.Done || outcome == PipelineOutcome.VerificationCompleted;
    }
  }
}
