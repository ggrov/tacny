using System;
using System.Collections.Generic;
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
    
    public static bool ToggleTacticEvaluation()
    {
      Translator.TacticEvaluationIsEnabled = !Translator.TacticEvaluationIsEnabled;
      return Translator.TacticEvaluationIsEnabled;
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

    public static bool GetExistingProgramFromBuffer(ITextBuffer buffer, out Program program) {
      program = null;
      Tuple<ITextSnapshot, Program, List<DafnyError>> parseResult;
      if (!buffer.Properties.TryGetProperty(bufferDafnyKey, out parseResult) || (parseResult.Item1 != buffer.CurrentSnapshot))
        return false;
      program = parseResult.Item2;
      return program!=null;
    }
  }
}
