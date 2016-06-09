using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
namespace Tacny {
  public class ProofState {

    public class Frame {
      private List<Statement> _body;
      public Dictionary<IVariable, object> DeclaredVariables;
      private Frame _parent;
      private int _bodyCounter;

      public int BodyCounter {
        get { return _bodyCounter; }
        set {
          Contract.Assert(_bodyCounter + value < _body.Count);
          _bodyCounter += value;
        }
      }

      public Frame(Frame parent) {
        _parent = parent;
        BodyCounter = 0;
      }
    }

    public Stack<Frame> Scope;
  }
}
