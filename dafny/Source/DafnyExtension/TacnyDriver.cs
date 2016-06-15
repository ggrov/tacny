using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LazyTacny;
using Microsoft.Boogie;
using Microsoft.VisualStudio.Text;
using Tacny;
using Util;
using Dafny = Microsoft.Dafny;

namespace DafnyLanguage
{
    public class TacnyDriver : ToolDriver
    {
        readonly string _filename;
        readonly ITextSnapshot _snapshot;
        readonly ITextBuffer _buffer;
        readonly List<DafnyError> _errors;
        public List<DafnyError> Errors { get { return _errors; } }
        public TacnyDriver(ITextBuffer buffer, string filename)
        {
            _buffer = buffer;
            _filename = filename;
            _snapshot = buffer.CurrentSnapshot;
            _errors = new List<DafnyError>();
        }
        internal override Dafny.Program ProcessResolution(bool runResolver)
        {
            var errorReporter = new VSErrorReporter(this);
            string[] args = new[] {_filename};
            Contract.Requires(tcce.NonNullElements(args));

            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;
            var options = new TacnyOptions { VerifySnapshots = 2 };
            TacnyOptions.Install(options);
            ExecutionEngine.printer = new ConsolePrinter();

            if (!CommandLineOptions.Clo.Parse(args))
            {
                RecordError(_filename, 1, 1, ErrorCategory.InternalError, "Tacny: Error processing Command Line Options");
                return null;
            }
            var tc = TacnyOptions.O;

            FieldInfo[] fields = tc.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {

                if (field.IsPublic)
                {
                    if (field.Name == "EnableSearch")
                    {
                        var val = field.GetValue(tc);
                        if (!(val is int)) continue;
                        var tmp = (int)val;
                        if (tmp < 0) continue;
                        var name = (Strategy)tmp;
                    }
                }

            }

            var fileNames = CommandLineOptions.Clo.Files;
            string programId = "main_program_id";
            Contract.Requires(tcce.NonNullElements(fileNames));

            Dafny.Program prog = null;
            using (new XmlFileScope(CommandLineOptions.Clo.XmlSink, fileNames[fileNames.Count - 1]))
            {
                Tacny.Program tacnyProgram;
                string programName = fileNames.Count == 1 ? fileNames[0] : "the program";
                try
                {
                    tacnyProgram = new Tacny.Program(fileNames, programId);
                }
                catch (ArgumentException ex)
                {
                    RecordError(_filename, 1, 1, ErrorCategory.InternalError, ex.Message+"\n"+ex.StackTrace);
                    return null;
                }

                if (!CommandLineOptions.Clo.NoResolve && !CommandLineOptions.Clo.NoTypecheck && Dafny.DafnyOptions.O.DafnyVerify)
                {
                    LazyTacny.Interpreter r = new LazyTacny.Interpreter(tacnyProgram);
                    prog = r.ResolveProgram();
                }
            }
            return prog;
        }

        void RecordError(string filename, int line, int col, ErrorCategory cat, string msg, bool isRecycled = false)
        {
            _errors.Add(new DafnyError(filename, line - 1, col - 1, cat, msg, _snapshot, isRecycled, null, System.IO.Path.GetFullPath(this._filename) == filename));
        }
        
        class VSErrorReporter : Dafny.ErrorReporter
        {
            TacnyDriver td;

            public VSErrorReporter(TacnyDriver td)
            {
                this.td = td;
            }

            // TODO: The error tracking could be made better to track the full information returned by Dafny
            public override bool Message(Dafny.MessageSource source, Dafny.ErrorLevel level, IToken tok, string msg)
            {
                if (base.Message(source, level, tok, msg))
                {
                    switch (level)
                    {
                        case Dafny.ErrorLevel.Error:
                            td.RecordError(tok.filename, tok.line, tok.col, source == Dafny.MessageSource.Parser ? ErrorCategory.ParseError : ErrorCategory.ResolveError, msg);
                            break;
                        case Dafny.ErrorLevel.Warning:
                            td.RecordError(tok.filename, tok.line, tok.col, source == Dafny.MessageSource.Parser ? ErrorCategory.ParseWarning : ErrorCategory.ResolveWarning, msg);
                            break;
                        case Dafny.ErrorLevel.Info:
                            // The AllMessages variable already keeps track of this
                            break;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
