﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Dfy = Microsoft.Dafny;
using Microsoft.Dafny;

namespace Tacny {
  public class ProofState {
    // Static State
    public readonly Dictionary<string, DatatypeDecl> Datatypes;
    public TopLevelClassDeclaration ActiveClass;
    private readonly List<TopLevelClassDeclaration> _topLevelClasses;
    private readonly Program _original;
    public List<TacticCache> ResultCache;

    // Dynamic State
    public Dictionary<string, VariableData> DafnyVariables;
    public MemberDecl TargetMethod;
    public ErrorReporter Reporter;

    //not all the eval step requires to be verified, e.g. var decl
    public bool IfVerify { set; get; } = false;

    public UpdateStmt TacticApplication;
    public ITactic ActiveTactic {
      get {
        Contract.Assert(_scope != null);
        Contract.Assert(_scope.Count > 0);
        return _scope.Peek().ActiveTactic;
      }
    }

    public TacticInformation TacticInfo {
      get {
        Contract.Assert(_scope != null);
        Contract.Assert(_scope.Count > 0);
        return _scope.Peek().TacticInfo;
      }
    }
    private Stack<Frame> _scope;

    public ProofState(Program program, ErrorReporter reporter, Program unresolvedProgram = null) {
      Contract.Requires(program != null);
      // get a new program instance
      Datatypes = new Dictionary<string, DatatypeDecl>();
      _topLevelClasses = new List<TopLevelClassDeclaration>();
      Reporter = reporter;
      if (unresolvedProgram == null) {
        //note the differences between this ParseCheck and the one at the top level. This function only parses but the other one resolves.
        var err = Parser.ParseOnly(new List<string>() { program.Name }, program.Name, out _original);
        if (err != null)
          reporter.Error(MessageSource.Tacny, program.DefaultModuleDef.tok, $"Error parsing a fresh Tacny program: {err}");
      } else {
        _original = unresolvedProgram ;
      }
      ResultCache = new List<TacticCache>();
      // fill state
      FillStaticState(program);
    }

    /// <summary>
    /// Initialize a new tactic state
    /// </summary>
    /// <param name="tacAps">Tactic application</param>
    /// <param name="variables">Dafny variables</param>
    public void InitState(UpdateStmt tacAps, Dictionary<IVariable, Dfy.Type> variables) {
      // clear the scope  
      _scope = new Stack<Frame>();
      var tactic = GetTactic(tacAps) as Tactic;

      var aps = ((ExprRhs) tacAps.Rhss[0]).Expr as ApplySuffix;
      if (aps.Args.Count != tactic.Ins.Count)
        Reporter.Error(MessageSource.Tacny, tacAps.Tok,
          $"Wrong number of method arguments (got {aps.Args.Count}, expected {tactic.Ins.Count})");
      var frame = new Frame(tactic, Reporter);
      for (int index = 0; index < aps.Args.Count; index++) {
        var arg = aps.Args[index];
        frame.AddLocalVariable(tactic.Ins[index].Name, arg);
      }

      _scope.Push(frame);
      FillSourceState(variables);
      TacticApplication = tacAps.Copy();
    }

    // Permanent state information
    public Dictionary<string, ITactic> Tactics => ActiveClass.Tactics;
    public Dictionary<string, MemberDecl> Members => ActiveClass.Members;


    public Program GetDafnyProgram() {
      //Contract.Requires(_original != null, "_original");
      Contract.Ensures(Contract.Result<Program>() != null);
      var copy = _original.Copy();
      return copy;
    }

    public Dictionary<IVariable, Dfy.Type> DafnyVars() {
      return DafnyVariables.ToDictionary(kvp => kvp.Value.Variable, kvp => kvp.Value.Type);
    }

    /// <summary>
    ///   Set active the enclosing TopLevelClass
    /// </summary>
    /// <param name="name"></param>
    public void SetTopLevelClass(string name) {
      ActiveClass = _topLevelClasses.FirstOrDefault(x => x.Name == name);
    }

    /// <summary>
    ///   Fill permanent state information, which will be common across all tactics
    /// </summary>
    /// <param name="program">fresh Dafny program</param>
    private void FillStaticState(Program program) {
      Contract.Requires<ArgumentNullException>(program != null);


      foreach (var item in program.DefaultModuleDef.TopLevelDecls) {
        var curDecl = item as ClassDecl;
        if (curDecl != null) {
          var temp = new TopLevelClassDeclaration(curDecl.Name);

          foreach (var member in curDecl.Members) {
            var tac = member as ITactic;
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
    ///   Fill the state information for the program member, from which the tactic call was made
    /// </summary>
    /// <param name="variables">Dictionary of key, key type pairs</param>
    /// <exception cref="ArgumentException">Variable has been declared in the context</exception>
    public void FillSourceState(Dictionary<IVariable, Dfy.Type> variables) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(variables));
      DafnyVariables = new Dictionary<string, VariableData>();
      foreach (var item in variables) {
        if (!DafnyVariables.ContainsKey(item.Key.Name))
          DafnyVariables.Add(item.Key.Name, new VariableData { Variable = item.Key, Type = item.Value });
        else
          throw new ArgumentException($"Dafny variable {item.Key.Name} is already declared in the current context");
      }
    }

    
    public void AddNewFrame(List<Statement> stmts, string kind = "default") {
      var parent = _scope.Peek();
      _scope.Push(new Frame(parent, stmts, kind));
    }


    public bool RemoveFrame() {
      // at least one frame in the proof state
      if (_scope.Count > 1){
        _scope.Pop();
        return true;
      }
      return false;
    }
   

    public void MarkCurFrameAsTerminated() {
      //assmeb code in the top frame
      _scope.Peek().MarkAsVerified();

      // add the assembled code to the parent frame
      if(_scope.Peek().Parent != null) {
        _scope.Peek().Parent.AddGeneratedCode(_scope.Peek().GetAssembledCode());
        _scope.Pop();
        if (_scope.Peek().IsTerminated())
          MarkCurFrameAsTerminated();
      }
    }

    // various getters
    #region GETTERS

    /// <summary>
    /// a proof state is verified if there is only one frame in the stack and _genratedCode is not null (raw code are assembled)
    /// </summary>
    /// <returns></returns>
    public bool IsVerified() {
      return _scope.Count == 1 && _scope.Peek().GetAssembledCode() != null;
    }

    /// <summary>
    /// Check if the current frame is fully interpreted by tracking counts of stmts
    /// </summary>
    /// <returns></returns>
    public bool IsEvaluated() {
      return _scope.Peek().IsEvaluated;
    }

    /// <summary>
    /// TODO
    /// </summary>
    /// <returns></returns>
    public bool IsPartiallyEvaluated() {
      return _scope.Peek().IsPartiallyEvaluated;
    }

    public string GetCurFrameTyp(){
      return _scope.Peek().WhatKind;
    }

    public List<Statement> GetGeneratedCode() {
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return _scope.Peek().GetGeneratedCode();
    }

    /// <summary>
    ///   Return Dafny key
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public IVariable GetVariable(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<IVariable>() != null);
      if (DafnyVariables.ContainsKey(key))
        return DafnyVariables[key].Variable;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    } 
    
    /// <summary>
    ///   Return Dafny key
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public IVariable GetVariable(NameSegment key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<IVariable>() != null);
      return GetVariable(key.Name);
    }

    /// <summary>
    ///   Check if Dafny key exists in the current context
    /// </summary>
    /// <param name="key">Variable</param>
    /// <returns>bool</returns>
    public bool ContainsVariable(NameSegment key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      return ContainsVariable(key.Name);
    }
    
    /// <summary>
    ///   Check if Dafny key exists in the current context
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    public bool ContainsVariable(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      return DafnyVariables.ContainsKey(key);
    }

    /// <summary>
    ///   Return the type of the key
    /// </summary>
    /// <param name="variable">key</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetVariableType(IVariable variable) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(variable));
      Contract.Ensures(Contract.Result<Dfy.Type>() != null);
      return GetVariableType(variable.Name);
    }

    /// <summary>
    ///   Return the type of the key
    /// </summary>
    /// <param name="key">name of the key</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetVariableType(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<Dfy.Type>() != null);
      if (DafnyVariables.ContainsKey(key))
        return DafnyVariables[key].Type;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    }

    /// <summary>
    /// Get the value of local variable
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object GetLocalValue(NameSegment key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Ensures(Contract.Result<object>() != null);
      return GetLocalValue(key.Name);
    }

    /// <summary>
    /// Get the value of local variable
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object GetLocalValue(string key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Ensures(Contract.Result<object>() != null);
      return _scope.Peek().GetLocalValue(key);
    }

    public bool HasLocalValue(NameSegment key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      return HasLocalValue(key.Name);
    }

    /// <summary>
    /// Check if Tacny variable has been declared
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool HasLocalValue(string key) {
      Contract.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), "key");
      return _scope.Peek().HasLocal(key);
    }

    private ITactic GetTactic(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      Contract.Requires<ArgumentNullException>(Tactics.ContainsKey(name), "Tactic does not exist in the current context");
      Contract.Ensures(Contract.Result<ITactic>() != null);

      return Tactics[name];
    }

    /// <summary>
    /// Get called tactic
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"> </exception>
    /// /// <exception cref="ArgumentException"> Provided UpdateStmt is not a tactic application</exception>
    public ITactic GetTactic(UpdateStmt us) {
      Contract.Requires(us != null);
      Contract.Requires<ArgumentException>(IsTacticCall(us));
      Contract.Ensures(Contract.Result<ITactic>() != null);
      return GetTactic(Util.GetSignature(us));
    }

    /// <summary>
    /// Get called tactic
    /// </summary>
    /// <param name="aps"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"> </exception>
    /// <exception cref="ArgumentException"> Provided ApplySuffix is not a tactic application</exception>
    public ITactic GetTactic(ApplySuffix aps) {
      Contract.Requires(aps != null);
      Contract.Requires(IsTacticCall(aps));
      Contract.Ensures(Contract.Result<ITactic>() != null);
      return GetTactic(Util.GetSignature(aps));
    }
    #endregion GETTERS

    // helper methods
    #region HELPERS
    /// <summary>
    ///   Check if an UpdateStmt is a tactic call
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    [Pure]
    public bool IsTacticCall(UpdateStmt us) {
      Contract.Requires(us != null);
      return IsTacticCall(Util.GetSignature(us));
    }

    /// <summary>
    ///   Check if an ApplySuffix is a tactic call
    /// </summary>
    /// <param name="aps"></param>
    /// <returns></returns>
    [Pure]
    public bool IsTacticCall(ApplySuffix aps) {
      Contract.Requires(aps != null);
      return IsTacticCall(Util.GetSignature(aps));
    }

    private bool IsTacticCall(string name) {
      Contract.Requires(tcce.NonNull(name));
      return Tactics.ContainsKey(name);
    }
    #endregion HELPERS

    /// <summary>
    /// Check in an updateStmt is local assignment
    /// </summary>
    /// <param name="us"></param>
    /// <returns></returns>
    [Pure]
    public bool IsLocalAssignment(UpdateStmt us) {
      if (us.Lhss.Count == 0)
        return false;
      foreach (var lhs in us.Lhss) {
        if (!(lhs is NameSegment))
          return false;
        if (!_scope.Peek().IsDeclared((lhs as NameSegment).Name))
          return false;
      }

      return true;
    }

    [Pure]
    public bool IsArgumentApplication(UpdateStmt us) {
      Contract.Requires<ArgumentNullException>(us != null, "us");
      var ns = Util.GetNameSegment(us);
      return _scope.Peek().HasLocal(ns);
    }


    /// <summary>
    /// Add a varialbe to the top level frame
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void AddLocal(IVariable key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Requires<ArgumentException>(!HasLocalValue(key.Name));
      AddLocal(key.Name, value);
    }

    /// <summary>
    /// Add a varialbe to the top level frame
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void AddLocal(string key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Requires<ArgumentException>(!HasLocalValue(key));
      _scope.Peek().AddLocalVariable(key, value);
    }

    /// <summary>
    /// Update a local variable
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void UpdateLocal(IVariable key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      UpdateLocal(key.Name, value);
    }

    /// <summary>
    /// Update a local variable
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void UpdateLocal(string key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      _scope.Peek().UpdateVariable(key, value);
    }

    /// <summary>
    /// Add new dafny stmt to the top frame
    /// </summary>
    /// <param name="stmt"></param>
    public void AddStatement(Statement stmt) {
      Contract.Requires<ArgumentNullException>(stmt != null, "stmt");
      _scope.Peek().AddGeneratedCode(stmt);
    }

    /// <summary>
    /// Add new dafny stmt to the top frame
    /// </summary>
    /// <param name="stmtList"></param>
    public void AddStatementRange(List<Statement> stmtList) {
      Contract.Requires<ArgumentNullException>(tcce.NonNullElements(stmtList));
      _scope.Peek().AddGeneratedCode(stmtList);
    }

    /// <summary>
    /// Return the latest unevalauted statement from the top frame
    /// </summary>
    /// <param name="partial"></param>
    /// <returns></returns>
    public Statement GetStmt(bool partial = false) {
      var stmt = _scope.Peek().CurStmt;
      if (!partial)
        _scope.Peek().IncCounter();
      return stmt;
    }

    public class TopLevelClassDeclaration {
      public readonly Dictionary<string, MemberDecl> Members;
      public readonly string Name;
      public readonly Dictionary<string, ITactic> Tactics;

      public TopLevelClassDeclaration(string name) {
        Contract.Requires(name != null);
        Tactics = new Dictionary<string, ITactic>();
        Members = new Dictionary<string, MemberDecl>();
        Name = name;
      }
    }

    internal class Frame {
      public readonly List<Statement> Body;
      // a control flag to determin how to assemly the generated code when popped.
      // by default, the stmt list will be returned and added into the parent frame
      // for some special blck, sucha as caseblock, it need to construct a new block stmt and then return
      // based on the BlcokKind, the sysytem will call the related TBlockStmtCOdeGenerator to handle this varation
      private int _bodyCounter;
      public readonly string WhatKind; 
      public Statement CurStmt => _bodyCounter >= Body.Count ? null : Body[_bodyCounter];
      public readonly Frame Parent;
      private readonly Dictionary<string, object> _declaredVariables;
      public readonly ITactic ActiveTactic;
      public bool IsPartiallyEvaluated { get; set; } = false;
      public bool IsEvaluated => _bodyCounter >= Body.Count;
      public TacticInformation TacticInfo;
      //a funtion with the right kind will be able to th generated code to List of statment
      private List<Statement> _generatedCode;
      //store the tempratry code to be combined, e.g. case statments for match
      private readonly List<List<Statement>> _rawCodeList;

      private readonly ErrorReporter _reporter;

      /// <summary>
      /// Initialize the top level frame
      /// </summary>
      /// <param name="tactic"></param>
      /// <param name="reporter"></param>
      public Frame(ITactic tactic, ErrorReporter reporter) {
        Contract.Requires<ArgumentNullException>(tactic != null, "tactic");
        Parent = null;
        var o = tactic as Tactic;
        if (o != null) Body = o.Body.Body;
        else {
          throw new NotSupportedException("tactic functions are not yet supported");
        }
        ActiveTactic = tactic;
        ParseTacticAttributes(((MemberDecl)ActiveTactic).Attributes);
        _reporter = reporter;
        _declaredVariables = new Dictionary<string, object>();
        _generatedCode = null;
        _rawCodeList = new List<List<Statement>>();
        WhatKind = "default";
      }

      public Frame(Frame parent, List<Statement> body, string kind) {
        Contract.Requires<ArgumentNullException>(parent != null);
        Contract.Requires<ArgumentNullException>(tcce.NonNullElements(body), "body");
        // carry over the tactic info
        TacticInfo = parent.TacticInfo;
        Body = body;
        _declaredVariables = new Dictionary<string, object>();
        Parent = parent;
        ActiveTactic = parent.ActiveTactic;
        _reporter = parent._reporter;
        _generatedCode = null;
        _rawCodeList = new List<List<Statement>>();
        WhatKind = kind;
      }

      public bool IncCounter() {
        _bodyCounter++;
        return _bodyCounter + 1 < Body.Count;
      }

      private void ParseTacticAttributes(Attributes attr) {
        // incase TacticInformation is not created
        TacticInfo = TacticInfo ?? new TacticInformation();
        if (attr == null)
          return;
        switch (attr.Name) {
          case "search":
            var expr = attr.Args.FirstOrDefault();
            string stratName = (expr as NameSegment)?.Name;
            Contract.Assert(stratName != null);
            try {
              TacticInfo.SearchStrategy = (Strategy)Enum.Parse(typeof(Strategy), stratName, true); // TODO: change to ENUM
            } catch {
              _reporter.Warning(MessageSource.Tacny, ((MemberDecl)ActiveTactic).tok, $"Unsupported search strategy {stratName}; Defaulting to DFS");
              TacticInfo.SearchStrategy = Strategy.Dfs;
            }
            break;
           case "partial":
            TacticInfo.IsPartial = true;
            break;
          default:
            _reporter.Warning(MessageSource.Tacny, ((MemberDecl)ActiveTactic).tok, $"Unrecognized attribute {attr.Name}");
            break;
        }

        if (attr.Prev != null)
          ParseTacticAttributes(attr.Prev);
      }

      [Pure]
      internal bool IsDeclared(string name) {
        Contract.Requires<ArgumentNullException>(name != null, "name");
        // base case
        if (Parent == null)
          return _declaredVariables.Any(kvp => kvp.Key == name);
        return _declaredVariables.Any(kvp => kvp.Key == name) || Parent.IsDeclared(name);
      }

      internal void AddLocalVariable(string variable, object value) {
        Contract.Requires<ArgumentNullException>(variable != null, "key");
        if (!_declaredVariables.Any(v => v.Key == variable)) {
          _declaredVariables.Add(variable, value);
        } else {
          throw new ArgumentException($"{variable} is already declared in the scope");
        }
      }

      internal void UpdateVariable(IVariable key, object value) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        Contract.Requires<ArgumentException>(IsDeclared(key.Name));//, $"{key} is not declared in the current scope".ToString());
        UpdateVariable(key.Name, value);
      }

      internal void UpdateVariable(string key, object value) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        Contract.Requires<ArgumentException>(IsDeclared(key));//, $"{key} is not declared in the current scope");
        // base case
        if (Parent == null) {
          // this is safe otherwise the contract would fail
          _declaredVariables[key] = value;
        } else {
          if (_declaredVariables.ContainsKey(key))
            _declaredVariables[key] = value;
          else {
            Parent.UpdateVariable(key, value);
          }
        }
      }

      internal object GetLocalValue(string key) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        Contract.Requires<ArgumentException>(IsDeclared(key));
        Contract.Ensures(Contract.Result<object>() != null);
        return Parent == null ? _declaredVariables[key] : Parent.GetLocalValue(key);
      }

      internal bool HasLocal(NameSegment key) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        return HasLocal(key.Name);
      }

      internal bool HasLocal(string key) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        return Parent?.HasLocal(key) ?? _declaredVariables.ContainsKey(key);
      }



      /// <summary>
      /// Add new dafny stmt to the current frame
      /// </summary>
      /// <param name="newStmt"></param>
      internal void AddGeneratedCode(Statement newStmt) {
        var l = new List<Statement>();
        l.Add(newStmt);
        _rawCodeList.Add(l);
      }

      /// <summary>
      /// Add new dafny stmt to the current frame
      /// </summary>
      /// <param name="newStmt"></param>
      internal void AddGeneratedCode(List<Statement> newStmt) {
        _rawCodeList.Add(newStmt);
      }

      /// <summary>
      /// assemble the list of stataemnt list (raw code) to statment list, depending on the current frame kind
      /// </summary>
      /// <returns></returns>
      internal static List<Statement> AssembleStmts(List<List<Statement>> raw, string whatKind){
        List<Statement> code;

        switch(whatKind) {
          case "tmatch":
            code = Tacny.Atomic.Match.Assemble(raw);          
            break;
          default:
            code = raw.SelectMany(x => x).ToList();
            break;
        }
        return code;
      }

      // only call it after verification is successful, this check if the current frame is terminated, after the poped child frame is termianted
      internal bool IsTerminated() {
        bool ret;

        switch(WhatKind) {
          case "tmatch":
            ret = Tacny.Atomic.Match.IsTerminated(_rawCodeList);
            break;
          default:
            ret = true;
            break;
        }
        return ret;
      }


      internal void MarkAsVerified() {
        _generatedCode = AssembleStmts(_rawCodeList, WhatKind);
      }

      internal List<Statement> GetAssembledCode(){
        return _generatedCode;
      }

      internal List<Statement> GetGeneratedCode(List<Statement> stmts = null){
        var code = GetGeneratedCode0(stmts);
        /*
        Printer p = new Printer(Console.Out);
        Console.WriteLine("--- Print out the generated code:");
        foreach (var x in code){
          p.PrintStatement(x, 1);
          Console.Write("\n");
        }
        Console.WriteLine("--- End of printing");
        */
        return code;
      }

      internal List<Statement> GetGeneratedCode0(List<Statement> stmts = null ) {
        Contract.Ensures(Contract.Result<List<Statement>>() != null);
        List<Statement> code;
        if (_generatedCode != null) // terminated, so just use the assembly code
          code = _generatedCode;
        else if (stmts != null){ // for the case when code are addded by child, and the child has assembly the code for parent
          code = stmts;
        }
        else{ // new code from child and not terminated, assmeble now
          code = AssembleStmts(_rawCodeList, WhatKind);
        }
          
        if(Parent == null)
          return code.Copy();
        else {
          // parent is always not yet terminated, so assmebly code for it
          var parRawCode = Parent._rawCodeList.Copy();
          parRawCode.Add(code);
          var parCode = AssembleStmts(parRawCode, Parent.WhatKind);
          return Parent.GetGeneratedCode0(parCode);
        }
      }
    }

    public class VariableData {
      private Dfy.Type _type;

      private IVariable _variable;

      public IVariable Variable {
        get { return _variable; }
        set {
          Contract.Assume(_variable == null); // key value should be only set once
          Contract.Assert(tcce.NonNull(value));
          _variable = value;
        }
      }

      public Dfy.Type Type {
        get { return _type; }
        set {
          Contract.Assume(_type == null);
          _type = value;
        }
      }
    }

    // tactic attribute information goes here
    public class TacticInformation {
      public Strategy SearchStrategy { get; set; } = Strategy.Dfs;
      public bool IsPartial { get; set; } = false;
    }

    public class TacticCache {
      public TacticCache(string name, Dictionary<UpdateStmt, List<Statement>> resultList) { 
        Name = name;
        ResultList = resultList;
      }
      public Dictionary<UpdateStmt, List<Statement>> ResultList { get; }
      /// <summary>
      /// Member name from which the tactic was called
      /// </summary>
      public readonly string Name;


    }
  }
}