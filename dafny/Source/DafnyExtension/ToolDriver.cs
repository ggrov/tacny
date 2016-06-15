using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace DafnyLanguage
{
    public abstract class ToolDriver
    {
        readonly string _filename;
        readonly ITextSnapshot _snapshot;
        readonly ITextBuffer _buffer;
        public readonly List<DafnyError> Errors;
        internal abstract Microsoft.Dafny.Program ProcessResolution(bool runResolver);
    }
}
