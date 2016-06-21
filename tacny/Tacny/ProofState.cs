using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dfy = Microsoft.Dafny;
using System.Linq;

namespace Tacny {
  public class ProofState {

    internal class TopLevelClassDeclaration {
      public readonly Dictionary<string, Dfy.ITactic> Tactics;
      public readonly Dictionary<string, MemberDecl> Members;
      public readonly string Name;


      public TopLevelClassDeclaration(string name) {
        Contract.Requires(name != null);
        Tactics = new Dictionary<string, Dfy.ITactic>();
        Members = new Dictionary<string, MemberDecl>();
        Name = name;

      }
    }

    internal class Frame {
      private readonly List<Statement> _body;
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

      public Frame(Frame parent, List<Statement> body) {
        _parent = parent;
        _body = body;
        BodyCounter = 0;
      }
    }

    public class VariableData {

      private IVariable _variable;
      public IVariable Variable {
        get { return _variable; }
        set {
          Contract.Assume(_variable == null); // variable value should be only set once
          Contract.Assert(tcce.NonNull(value));
          _variable = value;
        }
      }

      private Dfy.Type _type;

      public Dfy.Type Type {
        get {
          return _type;
        }
        set {
          Contract.Assume(_type == null);
          _type = value;
        }
      }
    }

    [ContractInvariantMethod]
    private void ObjectInvariant() {
      Contract.Invariant(_scope != null);
      Contract.Invariant(_currentTopLevelClass != null);
    }

    private Stack<Frame> _scope;
    private List<TopLevelClassDeclaration> _topLevelClasses;
    private TopLevelClassDeclaration _currentTopLevelClass;
    public Dictionary<string, VariableData> DafnyVariables;
    // Permanent state information
    public Dictionary<string, Dfy.ITactic> Tactics => _currentTopLevelClass.Tactics;
    public Dictionary<string, MemberDecl> Members => _currentTopLevelClass.Members;
    public readonly Dictionary<string, DatatypeDecl> Datatypes;



    public ProofState(Program program) {
      Contract.Requires(program != null);
      Datatypes = new Dictionary<string, DatatypeDecl>();
      // fill state
      FillStaticState(program);
    }

    /// <summary>
    /// Set active the enclosing TopLevelClass
    /// </summary>
    /// <param name="name"></param>
    public void SetTopLevelClass(string name) {
      _currentTopLevelClass = _topLevelClasses.FirstOrDefault(x => x.Name == name);
    }

    /// <summary>
    /// Fill permanent state information, which will be common across all tactics
    /// </summary>
    /// <param name="program">fresh Dafny program</param>
    private void FillStaticState(Program program) {
      Contract.Requires(program != null);
      _topLevelClasses = new List<TopLevelClassDeclaration>();

      foreach (var item in program.DefaultModuleDef.TopLevelDecls) {

        var curDecl = item as ClassDecl;
        if (curDecl != null) {
          var temp = new TopLevelClassDeclaration(curDecl.Name);

          foreach (var member in curDecl.Members) {
            var tac = member as Dfy.ITactic;
            if (tac != null)
              temp.Tactics.Add(tac.Name, tac);
            else {
              temp.Members.Add(member.Name, member);
            }
          }
          _topLevelClasses.Add(temp);
        } else {
          var dd = item as DatatypeDecl;
          if (dd != null)
            Datatypes.Add(dd.Name, dd);
        }
      }
    }

    /// <summary>
    /// Fill the state information for the program member, from which the tactic call was made
    /// </summary>
    /// <param name="variables">Dictionary of variable, variable type pairs</param>
    /// <exception cref="ArgumentException">Variable has been declared in the context</exception>
    private void FillSourceState(Dictionary<IVariable, Dfy.Type> variables) {
      Contract.Requires(tcce.NonNull(variables));
      DafnyVariables = new Dictionary<string, VariableData>();
      foreach (var item in variables) {
        if (!DafnyVariables.ContainsKey(item.Key.Name))
          DafnyVariables.Add(item.Key.Name, new VariableData() { Variable = item.Key, Type = item.Value });
        else
          throw new ArgumentException($"Dafny variable {item.Key.Name} is already declared in the current context");
      }
    }

    /// <summary>
    /// Return Dafny variable
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public IVariable GetVariable(string key) {
      Contract.Requires(tcce.NonNull(key));
      if (DafnyVariables.ContainsKey(key))
        return DafnyVariables[key].Variable;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    }

    /// <summary>
    /// Check if Dafny variable exists in the current context
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    public bool ContainsVariable(string key) {
      Contract.Requires(tcce.NonNull(key));
      return DafnyVariables.ContainsKey(key);
    }

    /// <summary>
    /// Return the type of the variable
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetVariableType(IVariable variable) {
      Contract.Requires(tcce.NonNull(variable));
      return GetVariableType(variable.Name);
    }

    /// <summary>
    /// Return the type of the variable
    /// </summary>
    /// <param name="key">name of the variable</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetVariableType(string key) {
      Contract.Requires(tcce.NonNull(key));
      if (DafnyVariables.ContainsKey(key))
        return DafnyVariables[key].Type;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    }

    /// <summary>
    /// Return the string signature of an UpdateStmt
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    public static string GetSignature(UpdateStmt us) {
      var er = us.Rhss[0] as ExprRhs;
      return er == null ? null : GetSignature(er.Expr as ApplySuffix);
    }

    /// <summary>
    /// Return the string signature of an ApplySuffix
    /// </summary>
    /// <param name="aps"></param>
    /// <returns></returns>
    public static string GetSignature(ApplySuffix aps) {
      return aps?.Lhs.tok.val;
    }

    /// <summary>
    /// Check if an UpdateStmt is a tactic call
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    [Pure]
    public bool IsTacticCall(UpdateStmt us) {
      Contract.Requires(us != null);
      return IsTacticCall(GetSignature(us));
    }

    /// <summary>
    /// Check if an ApplySuffix is a tactic call
    /// </summary>
    /// <param name="aps"></param>
    /// <returns></returns>
    [Pure]
    public bool IsTacticCall(ApplySuffix aps) {
      Contract.Requires(aps != null);
      return IsTacticCall(GetSignature(aps));
    }

    private bool IsTacticCall(string name) {
      return name != null && Tactics.ContainsKey(name);
    }
  }
}
