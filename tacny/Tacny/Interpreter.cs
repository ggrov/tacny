using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace Tacny {
  public class Interpreter {

    private static Interpreter _i;

    private ProofState _state;
    private Stack<Dictionary<IVariable, Type>> _frame;


    [ContractInvariantMethod]
    private void ObjectInvariant() {
      Contract.Invariant(tcce.NonNull(_state));
      Contract.Invariant(tcce.NonNull(_frame));
    }

    public static MemberDecl ApplyTactic(Program program, MemberDecl target) {
      Contract.Requires(program != null);
      Contract.Requires(target != null);
      if (_i == null) {
        _i = new Interpreter(program);
      }
      return _i.Interpret(target);
    }


    public Interpreter(Program program) {
      Contract.Requires(tcce.NonNull(program));
      // initialize state
      _state = new ProofState(program);
    }



    private MemberDecl Interpret(MemberDecl target) {
      Contract.Requires(tcce.NonNull(target));
      // initialize new frame for variables
      _frame = new Stack<Dictionary<IVariable, Type>>();
      var method = target as Method;
      if (method != null) {
        _state.SetTopLevelClass(method.EnclosingClass?.Name);
        var dict = method.Ins.Concat(method.Outs)
          .ToDictionary<IVariable, IVariable, Type>(item => item, item => item.Type);
        _frame.Push(dict);
        FindTactic(method.Body);
        dict = _frame.Pop();
        // sanity check
        Contract.Assert(_frame.Count == 0);

      }
      return null;
    }

    // Find tactic application and resolve it
    private void FindTactic(BlockStmt body) {
      Contract.Requires(tcce.NonNull(body));
      Contract.Requires(tcce.NonNull(_frame));

      var dict = new Dictionary<IVariable, Type>();
      for(var i = 0; i<body.Body.Count; i++) {
        var stmt = body.Body[i];
        if (stmt is VarDeclStmt) {
          var vds = stmt as VarDeclStmt;
          // register local variable declarations
          foreach (var local in vds.Locals) {
            try {
              dict.Add(local, local.Type);
            } catch (Exception e) {
              //TODO: some error handling when target is not resolved
              Console.Out.WriteLine(e.Message);
            }
          }
        } else if (stmt is IfStmt) {
          var ifStmt = stmt as IfStmt;
          _frame.Push(dict);
          FindTactic(ifStmt);
          // remove the top _frame
          dict = _frame.Pop();
        } else if (stmt is WhileStmt) {
          var whileStmt = stmt as WhileStmt;
          _frame.Push(dict);
          FindTactic(whileStmt.Body);
          dict = _frame.Pop();
        } else if (stmt is UpdateStmt) {
          var us = stmt as UpdateStmt;
          if (_state.IsTacticCall(us)) {
            var list = MergeFrame(_frame);

            body.Body.InsertRange(i, body.Body);
          }
        }
      }
    }

    private void FindTactic(IfStmt ifStmt) {
      Contract.Requires(tcce.NonNull(ifStmt));
      Contract.Requires(tcce.NonNull(_frame));

      FindTactic(ifStmt.Thn);
      if (ifStmt.Els == null) return;
      var els = ifStmt.Els as BlockStmt;
      if (els != null) {
        FindTactic(els);
      } else if (ifStmt.Els is IfStmt) {
        FindTactic((IfStmt) ifStmt.Els);
      }
    }

    private static Dictionary<IVariable, Type> MergeFrame(Stack<Dictionary<IVariable, Type>> frame) {
      var dict = new Dictionary<IVariable, Type>();

      return frame.Aggregate(dict, (current, item) => current.Concat(item).ToDictionary(x => x.Key, x => x.Value));
    }

    private void ApplyTactic(Dictionary<IVariable, Type> variables, UpdateStmt tacticApplication) {
      
    }

  }
}
