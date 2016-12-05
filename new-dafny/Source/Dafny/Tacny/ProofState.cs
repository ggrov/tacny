using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Dfy = Microsoft.Dafny;
using Microsoft.Dafny;
using Tacny.Language;

namespace Tacny {
  public class ProofState {
    // Static State
    public readonly Dictionary<string, DatatypeDecl> Datatypes;
    public TopLevelClassDeclaration ActiveClass;
    private readonly List<TopLevelClassDeclaration> _topLevelClasses;
    private readonly Program _original;

    // Dynamic State
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

    public CtrlInfo FrameCtrlInfo {
      get {
        Contract.Assert(_scope != null);
        Contract.Assert(_scope.Count > 0);
        return _scope.Peek().FrameCtrlInfo;
      }
    }
    private Stack<Frame> _scope;

    public ProofState(Program program, ErrorReporter reporter) {
      Contract.Requires(program != null);
      // get a new program instance
      Datatypes = new Dictionary<string, DatatypeDecl>();
      _topLevelClasses = new List<TopLevelClassDeclaration>();
      Reporter = reporter;

      //note the differences between this ParseCheck and the one at the top level. This function only parses but the other one resolves.
      var err = Parser.ParseOnly(new List<string>() { program.FullName }, program.Name, out _original);
      if (err != null)
        reporter.Error(MessageSource.Tacny, program.DefaultModuleDef.tok, $"Error parsing a fresh Tacny program: {err}");
    
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

      var aps = ((ExprRhs)tacAps.Rhss[0]).Expr as ApplySuffix;
      if(aps.Args.Count != tactic.Ins.Count)
        Reporter.Error(MessageSource.Tacny, tacAps.Tok,
          $"Wrong number of method arguments (got {aps.Args.Count}, expected {tactic.Ins.Count})");

      var frame = new Frame(tactic, Reporter);

      foreach(var item in variables) {
        if(!frame.ContainDafnyVar(item.Key.Name))
          frame.AddDafnyVar(item.Key.Name, new VariableData { Variable = item.Key, Type = item.Value });
        else
          throw new ArgumentException($"Dafny variable {item.Key.Name} is already declared in the current context");
      }

      for(int index = 0; index < aps.Args.Count; index++) {
        var arg = aps.Args[index];
        frame.AddTacnyVar(tactic.Ins[index].Name, arg);
      }

      _scope.Push(frame);
      TacticApplication = tacAps.Copy();
    }

    public void InitTacFrame(UpdateStmt tacAps, bool partial) {
      var aps = ((ExprRhs)tacAps.Rhss[0]).Expr as ApplySuffix;
      var tactic = GetTactic(tacAps) as Tactic;

      if(aps.Args.Count != tactic.Ins.Count)
        Reporter.Error(MessageSource.Tacny, tacAps.Tok,
          $"Wrong number of method arguments (got {aps.Args.Count}, expected {tactic.Ins.Count})");

      AddNewFrame(tactic.Body.Body, partial);
      for(int index = 0; index < aps.Args.Count; index++) {
        var arg = aps.Args[index];

        if (arg is Microsoft.Dafny.NameSegment) {
        var name = ((Microsoft.Dafny.NameSegment)arg).Name;
        if(ContainTacnyVal(name))
          // in the case that referring to an exisiting tvar, dereference it
         arg = GetTacnyVarValue(name) as Expression;
        else{
          Reporter.Error(MessageSource.Tacny, tacAps.Tok,
            $"Fail to dereferenen argument({name})");
        }
      }

      _scope.Peek().AddTacnyVar(tactic.Ins[index].Name, arg);


      }
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

    
    public void AddNewFrame(List<Statement> stmts, bool partial, string kind = "default") {
      var parent = _scope.Peek();
      _scope.Push(new Frame(parent, stmts, partial, kind));
    }

    public void MarkCurFrameAsTerminated(bool curFrameProved) {
      //assmeb code in the top frame
      _scope.Peek().MarkAsEvaluated(curFrameProved);

      var code = _scope.Peek().GetFinalCode();

      // add the assembled code to the parent frame
      if(code!= null && _scope.Peek().Parent != null) {
        _scope.Peek().Parent.AddGeneratedCode(code);
        _scope.Pop();
        if (_scope.Peek().IsFrameTerminated(curFrameProved))
          MarkCurFrameAsTerminated(curFrameProved);
      }
    }

    // various getters
    #region GETTERS

    public bool IsCurFramePartial(){
      return _scope.Peek().FrameCtrlInfo.IsPartial;
    }
    /// <summary>
    /// a proof state is verified if there is only one frame in the stack and _genratedCode is not null (raw code are assembled)
    /// </summary>
    /// <returns></returns>
    public bool IsTerminated(){
      return _scope.Count == 1 && _scope.Peek().GetFinalCode() != null;
    }

    /// <summary>
    /// Check if the current frame is fully interpreted by tracking counts of stmts
    /// </summary>
    /// <returns></returns>
    public bool IsEvaluated() {
      return _scope.Peek().IsEvaluated;
    }


    public string GetCurFrameTyp(){
      return _scope.Peek().WhatKind;
    }

    public List<Statement> GetGeneratedCode() {
     // Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return _scope.Peek().GetGeneratedCode();
    }

    public List<List<Statement>> GetGeneratedaRawCode() {
     // Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return _scope.Peek().GetRawCode();
    }


    /// <summary>
    ///   Check if Dafny key exists in the current context
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    public bool ContainDafnyVar(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      return _scope.Peek().ContainDafnyVar(key);
    }


    /// <summary>
    ///   Check if Dafny key exists in the current context
    /// </summary>
    /// <param name="key">Variable</param>
    /// <returns>bool</returns>

   public bool ContainDafnyVar(NameSegment key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      return ContainDafnyVar(key.Name);
    }

    /// <summary>
    ///   Return Dafny key
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public IVariable GetDafnyVar(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<IVariable>() != null);
      if(ContainDafnyVar(key))
        return _scope.Peek().GetDafnyVariableData(key).Variable;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    } 
    
    /// <summary>
    ///   Return Dafny key
    /// </summary>
    /// <param name="key">Variable name</param>
    /// <returns>bool</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public IVariable GetDafnyVar(NameSegment key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<IVariable>() != null);
      return GetDafnyVar(key.Name);
    }
    /// <summary>
    /// get a dictionary containing all the dafny variable in current scope, including all the frame. If the variable will be ignore, if it confilts with some other top frames
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, VariableData> GetAllDafnyVars() {
      return _scope.Peek().GetAllDafnyVars(new Dictionary<string, VariableData>());
    }

    /// <summary>
    ///   Return the type of the key
    /// </summary>
    /// <param name="variable">key</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetDafnyVarType(IVariable variable) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(variable));
      Contract.Ensures(Contract.Result<Dfy.Type>() != null);
      return GetDafnyVarType(variable.Name);
    }

    /// <summary>
    ///   Return the type of the key
    /// </summary>
    /// <param name="key">name of the key</param>
    /// <returns>null if type is not known</returns>
    /// <exception cref="KeyNotFoundException">Variable does not exist in the current context</exception>
    public Dfy.Type GetDafnyVarType(string key) {
      Contract.Requires<ArgumentNullException>(tcce.NonNull(key));
      Contract.Ensures(Contract.Result<Dfy.Type>() != null);
      if (ContainDafnyVar(key))
        return GetDafnyVar(key).Type;
      throw new KeyNotFoundException($"Dafny variable {key} does not exist in the current context");
    }

    /// <summary>
    /// Get the value of local variable
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object GetTacnyVarValue(NameSegment key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Ensures(Contract.Result<object>() != null);
      return GetTacnyVarValue(key.Name);
    }

    /// <summary>
    /// Get the value of local variable
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object GetTacnyVarValue(string key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Ensures(Contract.Result<object>() != null);
      return _scope.Peek().GetTacnyValData(key);
    }

    public bool ContainTacnyVal(NameSegment key) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      return ContainTacnyVal(key.Name);
    }

    /// <summary>
    /// Check if Tacny variable has been declared
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainTacnyVal(string key) {
      Contract.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), "key");
      return _scope.Peek().ContainTacnyVars(key);
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
        if (!_scope.Peek().ContainTacnyVars((lhs as NameSegment).Name))
          return false;
      }

      return true;
    }

    [Pure]
    public bool IsArgumentApplication(UpdateStmt us) {
      Contract.Requires<ArgumentNullException>(us != null, "us");
      var ns = Util.GetNameSegment(us);
      return _scope.Peek().ContainTacnyVars(ns.Name);
    }

    /// <summary>
    /// Add a varialbe to the top level frame
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void AddTacnyVar(IVariable key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Requires<ArgumentException>(!ContainTacnyVal(key.Name));
      AddTacnyVar(key.Name, value);
    }

    /// <summary>
    /// Add a varialbe to the top level frame
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void AddTacnyVar(string key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      Contract.Requires<ArgumentException>(!ContainTacnyVal(key));
      _scope.Peek().AddTacnyVar(key, value);
    }

    /// <summary>
    /// Update a local variable
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void UpdateTacnyVar(IVariable key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      UpdateTacnyVar(key.Name, value);
    }

    /// <summary>
    /// Update a local variable
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void UpdateTacnyVar(string key, object value) {
      Contract.Requires<ArgumentNullException>(key != null, "key");
      _scope.Peek().UpdateLocalTacnyVar(key, value);
    }

    /// <summary>
    /// add a dafny variable to the top frame
    /// </summary>
    /// <param name="name"></param>
    /// <param name="var"></param>
    public void AddDafnyVar(string name, VariableData var){
      _scope.Peek().AddDafnyVar(name, var);
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
      private readonly Dictionary<string, object> _declaredVariables; // tacny variables
      private readonly Dictionary<string, VariableData> _DafnyVariables; // dafny variables
      public readonly ITactic ActiveTactic;
      public bool IsEvaluated => _bodyCounter >= Body.Count;
      public CtrlInfo FrameCtrlInfo; // tactic and flowcontrol info
      //a funtion with the right kind will be able to th generated code to List of statment
      private List<Statement> _generatedCode;
      //store the tempratry code to be combined, e.g. case statments for match, wit a boolean tag indicating whether is verified
      //private readonly List<Tuple<bool, List<Statement>>> _rawCodeList;
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
        _DafnyVariables = new Dictionary<string, VariableData>();
        _generatedCode = null;
        _rawCodeList = new List<List<Statement>>();
        WhatKind = "default";
      }

      public Frame(Frame parent, List<Statement> body, bool partial, string kind) {
        Contract.Requires<ArgumentNullException>(parent != null);
        Contract.Requires<ArgumentNullException>(tcce.NonNullElements(body), "body");
        // carry over the tactic info
        Body = body;
        _declaredVariables = new Dictionary<string, object>();
        _DafnyVariables = new Dictionary<string, VariableData>();
        Parent = parent;
        ActiveTactic = parent.ActiveTactic;
        _reporter = parent._reporter;
        _generatedCode = null;
        _rawCodeList = new List<List<Statement>>();
        WhatKind = kind;
        FrameCtrlInfo = parent.FrameCtrlInfo;
        FrameCtrlInfo.IsPartial = FrameCtrlInfo.IsPartial || partial;
      }

      public bool IncCounter() {
        _bodyCounter++;
        return _bodyCounter + 1 < Body.Count;
      }

      private void ParseTacticAttributes(Attributes attr) {
        // incase TacticInformation is not created
        FrameCtrlInfo = FrameCtrlInfo ?? new CtrlInfo();
        if (attr == null)
          return;
        switch (attr.Name) {
          case "search":
            var expr = attr.Args.FirstOrDefault();
            string stratName = (expr as NameSegment)?.Name;
            Contract.Assert(stratName != null);
            try {
              FrameCtrlInfo.SearchStrategy = (Strategy)Enum.Parse(typeof(Strategy), stratName, true); // TODO: change to ENUM
            } catch {
              _reporter.Warning(MessageSource.Tacny, ((MemberDecl)ActiveTactic).tok, $"Unsupported search strategy {stratName}; Defaulting to DFS");
              FrameCtrlInfo.SearchStrategy = Strategy.Dfs;
            }
            break;
           case "partial":
            FrameCtrlInfo.IsPartial = true;
            break;
          default:
            //_reporter.Warning(MessageSource.Tacny, ((MemberDecl)ActiveTactic).tok, $"Unrecognized attribute {attr.Name}");
            break;
        }

        if (attr.Prev != null)
          ParseTacticAttributes(attr.Prev);
      }

      [Pure]
      internal VariableData GetLocalDafnyVar(string name) {
        //Contract.Requires(_DafnyVariables.ContainsKey(name));
        return _DafnyVariables[name];
      }

      internal void AddDafnyVar(string name, VariableData var) {
        Contract.Requires<ArgumentNullException>(name != null, "key");
        if(_DafnyVariables.All(v => v.Key != name)) {
          _DafnyVariables.Add(name, var);
        } else {
          throw new ArgumentException($"dafny var {name} is already declared in the scope");
        }
      }

      internal bool ContainDafnyVar(string name) {
        Contract.Requires<ArgumentNullException>(name != null, "name");
        // base case
        if(Parent == null)
          return _DafnyVariables.Any(kvp => kvp.Key == name);
        return _DafnyVariables.Any(kvp => kvp.Key == name) || Parent.ContainDafnyVar(name);
      }


      internal VariableData GetDafnyVariableData(string name){
//     Contract.Requires(ContainDafnyVars(name));
        if (_DafnyVariables.ContainsKey(name))
          return _DafnyVariables[name];
        else{
          return Parent.GetDafnyVariableData(name);
        }
      }

      internal Dictionary<string, VariableData> GetAllDafnyVars(Dictionary<string, VariableData> toDict){
        _DafnyVariables.Where(x => !toDict.ContainsKey(x.Key)).ToList().ForEach(x => toDict.Add(x.Key, x.Value));
        if (Parent == null)
          return toDict;
        else{
          return Parent.GetAllDafnyVars(toDict);
        }
      }


      internal bool ContainTacnyVars(string name) {
        Contract.Requires<ArgumentNullException>(name != null, "name");
        // base case
        if (Parent == null)
          return _declaredVariables.Any(kvp => kvp.Key == name);
        return _declaredVariables.Any(kvp => kvp.Key == name) || Parent.ContainTacnyVars(name);
      }

      internal void AddTacnyVar(string variable, object value) {
        Contract.Requires<ArgumentNullException>(variable != null, "key");
        if (_declaredVariables.All(v => v.Key != variable)) {
          _declaredVariables.Add(variable, value);
        } else {
          throw new ArgumentException($"tacny var {variable} is already declared in the scope");
        }
      }

      internal void UpdateLocalTacnyVar(IVariable key, object value) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        Contract.Requires<ArgumentException>(ContainTacnyVars(key.Name));//, $"{key} is not declared in the current scope".ToString());
        UpdateLocalTacnyVar(key.Name, value);
      }

      internal void UpdateLocalTacnyVar(string key, object value) {
        Contract.Requires<ArgumentNullException>(key != null, "key");
        //Contract.Requires<ArgumentException>(_declaredVariables.ContainsKey(key));
         _declaredVariables[key] = value;
      }

      internal object GetTacnyValData(string name) {
        Contract.Requires<ArgumentNullException>(name != null, "key");
        //Contract.Requires<ArgumentException>(ContainTacnyVars(key));
        Contract.Ensures(Contract.Result<object>() != null);
        if (_declaredVariables.ContainsKey(name))
          return _declaredVariables[name];
        else{
          return Parent.GetTacnyValData(name);
        }
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
            code = Match.Assemble(raw);          
            break;
          default:
            code = raw.SelectMany(x => x).ToList();
            break;
        }
        return code;
      }

      // only call it when verification is successful, this check if the current frame is terminated, 
      // when the popped child frame is termianted
      internal bool IsFrameTerminated(bool latestChildFrameRes) {
        bool ret;

        switch(WhatKind) {
          case "tmatch":
            ret = Match.IsTerminated(_rawCodeList, latestChildFrameRes);
            break;
          default:
            ret = latestChildFrameRes;
            break;
        }
        return ret;
      }

      /// <summary>
      /// this will assemble the raw code if the raw code can be verified or parital is allowed
      /// </summary>
      internal void MarkAsEvaluated(bool curFrameProved) {
        if (curFrameProved || FrameCtrlInfo.IsPartial){
          _generatedCode = AssembleStmts(_rawCodeList, WhatKind);
        }
      }

      internal List<List<Statement>> GetRawCode(){
        return _rawCodeList;
      }
      internal List<Statement> GetFinalCode(){
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
    public class CtrlInfo {
      public Strategy SearchStrategy { get; set; } = Strategy.Dfs;
      public bool IsPartial { get; set; } = false;
    }


  }
}