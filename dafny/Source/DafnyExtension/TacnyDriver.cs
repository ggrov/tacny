using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Tacny = Tacny;

namespace DafnyLanguage
{
    public class TacnyDriver
    {
        readonly string _filename;
        public TacnyDriver(string filename)
        {
            _filename = filename;
        }
    }
}
