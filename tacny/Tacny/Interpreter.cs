using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace Tacny {
  public class Interpreter {

    private static Interpreter _i;
    
    
    public static MemberDecl ApplyTactic(Program program, MemberDecl target) {
      if (_i == null) {
        _i = new Interpreter(program);
      }
      return _i.Interpret(target);
    }


    public Interpreter(Program program) {
      // initialize state here
    }



    private MemberDecl Interpret(MemberDecl target) {
      // create 
      return null;
    }
  }
}
