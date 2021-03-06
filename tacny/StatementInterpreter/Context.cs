﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Dafny;
using Util;
using Type = Microsoft.Dafny.Type;

namespace Tacny {
  public class Context {
    public MemberDecl md;
    public UpdateStmt tac_call;

    public Context() {
    }
    public Context(MemberDecl md, UpdateStmt tac_call) {
      Contract.Requires(md != null && tac_call != null);
      this.md = md;
      this.tac_call = tac_call;
    }

  }

  /// <summary>
  /// Local context for the tactic currently being resolved
  /// </summary>
  #region LocalContext
  public class DynamicContext : Context {
    public ITactic tactic;  // The called tactic
    public List<Statement> tacticBody = new List<Statement>(); // body of the currently worked tactic
    public Dictionary<IVariable, object> localDeclarations = new Dictionary<IVariable, object>();
    public Dictionary<Statement, Statement> generatedStatements = new Dictionary<Statement, Statement>();
    public List<Expression> generatedExpressions = new List<Expression>();
    public MemberDecl newTarget;
    public DatatypeCtor activeCtor;
    public WhileStmt whileStmt;
    private int tacCounter;
    public bool isPartialyResolved;
    public DynamicContext() {

    }
    public DynamicContext(MemberDecl md, ITactic tac, UpdateStmt tac_call)
        : base(md, tac_call) {
      tactic = tac;
      if (tactic is Tactic) {
        var tmp = tactic as Tactic;
        tacticBody = new List<Statement>(tmp.Body.Body.ToArray());
      }
      tacCounter = 0;
      FillTacticInputs();
    }

    public DynamicContext(MemberDecl md, ITactic tac, UpdateStmt tac_call,
        List<Statement> tac_body, Dictionary<IVariable, object> local_variables,
        Dictionary<Statement, Statement> updated_statements, int tacCounter, MemberDecl old_target)
        : base(md, tac_call) {
      tactic = tac;
      tacticBody = new List<Statement>(tac_body.ToArray());

      List<IVariable> lv_keys = new List<IVariable>(local_variables.Keys);
      List<object> lv_values = new List<object>(local_variables.Values);
      localDeclarations = lv_keys.ToDictionary(x => x, x => lv_values[lv_keys.IndexOf(x)]);

      generatedStatements = updated_statements;

      this.tacCounter = tacCounter;
      newTarget = old_target;
    }

    public DynamicContext Copy() {
      var newM = Util.Copy.CopyMember(md);
      ITactic newTac = Util.Copy.CopyMember(tactic as MemberDecl) as ITactic;
      var new_target = newTarget != null ? Util.Copy.CopyMember(newTarget) : null;
      var newContext = new DynamicContext(newM, newTac, tac_call, tacticBody, localDeclarations, Util.Copy.CopyStatementDict(generatedStatements), tacCounter, new_target);
      newContext.activeCtor = activeCtor;
      newContext.isPartialyResolved = isPartialyResolved;
      newContext.whileStmt = whileStmt;
      return newContext;
    }


    /// <summary>
    /// Clear local variables, and fill them with tactic arguments. Use with caution.
    /// </summary>
    public void FillTacticInputs() {
      localDeclarations.Clear();
      ExprRhs er = (ExprRhs)tac_call.Rhss[0];
      List<Expression> exps = ((ApplySuffix)er.Expr).Args;
      Contract.Assert(exps.Count == tactic.Ins.Count);
      for (int i = 0; i < exps.Count; i++)
        localDeclarations.Add(tactic.Ins[i], exps[i]);
    }

    public bool HasLocalWithName(NameSegment ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      List<IVariable> ins = new List<IVariable>(localDeclarations.Keys);
      var key = ins.FirstOrDefault(i => i.Name == ns.Name);
      return key != null;
    }

    public object GetLocalValueByName(NameSegment ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return GetLocalValueByName(ns.Name);
    }

    public object GetLocalValueByName(IVariable ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return GetLocalValueByName(ns.Name);
    }

    public object GetLocalValueByName(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      List<IVariable> ins = new List<IVariable>(localDeclarations.Keys);
      return localDeclarations.FirstOrDefault(i => i.Key.Name == name).Value;
    }

    public IVariable GetLocalKeyByName(IVariable ns) {
      Contract.Requires<ArgumentNullException>(ns != null);
      return GetLocalKeyByName(ns.Name);
    }

    public IVariable GetLocalKeyByName(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      List<IVariable> ins = new List<IVariable>(localDeclarations.Keys);
      return ins.FirstOrDefault(i => i.Name == name);
    }

    public void AddLocal(IVariable lv, object value) {
      Contract.Requires<ArgumentNullException>(lv != null);
      if (!localDeclarations.ContainsKey(lv))
        localDeclarations.Add(lv, value);
      else
        localDeclarations[lv] = value;
    }



    public void IncCounter() {
      if (tacCounter <= tacticBody.Count)
        tacCounter++;
      else
        throw new ArgumentOutOfRangeException("Tactic counter exceeded tactic body length");
    }

    public void DecCounter() {
      tacCounter--;
    }

    public void ResetCounter() {
      tacCounter = 0;
    }

    public void SetCounter(int val) {
      tacCounter = val;
    }

    public int GetCounter() {
      return tacCounter;
    }

    public Statement GetCurrentStatement() {
      if (tacCounter >= tacticBody.Count)
        return null;

      return tacticBody[tacCounter];
    }

    public Statement GetNextStatement() {
      if (tacCounter + 1 >= tacticBody.Count)
        return null;
      return tacticBody[tacCounter + 1];
    }

    public bool IsFirstStatment() {
      return tacCounter == 0;
    }

    public bool IsResolved() {
      return tacCounter >= tacticBody.Count;
    }

    public void AddUpdated(Statement key, Statement value) {
      Contract.Requires(key != null && value != null);
      if (!generatedStatements.ContainsKey(key))
        generatedStatements.Add(key, value);
      else
        generatedStatements[key] = value;
    }

    public void RemoveUpdated(Statement key) {
      Contract.Requires(key != null);
      if (generatedStatements.ContainsKey(key))
        generatedStatements.Remove(key);
    }

    public Statement GetUpdated(Statement key) {
      Contract.Requires(key != null);
      if (generatedStatements.ContainsKey(key))
        return generatedStatements[key];
      return null;
    }

    public List<Statement> GetAllUpdated() {
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return new List<Statement>(generatedStatements.Values.ToArray());
    }

    public List<Statement> GetFreshTacticBody() {
      Contract.Ensures(Contract.Result<List<Statement>>() != null);
      return new List<Statement>(tacticBody.ToArray());
    }

    public Method GetSourceMethod() {
      return md as Method;
    }
  }
  #endregion

  #region GlobalContext

  public class StaticContext : Context {
    public readonly Dictionary<string, DatatypeDecl> datatypes = new Dictionary<string, DatatypeDecl>();
    public Dictionary<string, IVariable> staticVariables = new Dictionary<string, IVariable>();
    public Dictionary<IVariable, Type> variable_types = new Dictionary<IVariable, Type>();
    //public Dictionary<string, IVariable> temp_variables = new Dictionary<string, IVariable>();
    public List<Statement> resolved = new List<Statement>();
    public MemberDecl newTarget = null;
    public Program program;

    public StaticContext() {

    }
    public StaticContext(MemberDecl md, UpdateStmt tac_call, Program program)
        : base(md, tac_call) {
      this.program = program;
      foreach (DatatypeDecl tld in program.Globals)
        datatypes.Add(tld.Name, tld);
    }

    public bool ContainsGlobalKey(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      return datatypes.ContainsKey(name);
    }

    public DatatypeDecl GetGlobal(string name) {
      Contract.Requires<ArgumentNullException>(name != null);
      return ContainsGlobalKey(name) ? datatypes[name] : null;
    }

    // register global variables and their asociated types
    public void RegsiterGlobalVariables(List<IVariable> globals, List<IVariable> resolved = null) {
      Contract.Requires(globals != null);
      foreach (var item in globals) {
        if (!staticVariables.ContainsKey(item.Name))
          staticVariables.Add(item.Name, item);
        else
          staticVariables[item.Name] = item;

        if (resolved != null) {
          var tmp = resolved.FirstOrDefault(i => i.Name == item.Name);
          if (tmp != null) {
            if (!variable_types.ContainsKey(item))
              variable_types.Add(item, tmp.Type);
            else
              variable_types[item] = item.Type;
          }
        }
      }
    }

    // register one global variable and asociated type
    public void RegsiterGlobalVariable(IVariable variable) {
      Contract.Requires<ArgumentNullException>(variable != null);

      if (!staticVariables.ContainsKey(variable.Name))
        staticVariables.Add(variable.Name, variable);
      else
        staticVariables[variable.Name] = variable;

      if (variable.Type != null) {
        if (!variable_types.ContainsKey(variable))
          variable_types.Add(variable, variable.Type);
        else
          variable_types[variable] = variable.Type;
      }
    }

    public void RemoveGlobalVariable(IVariable variable) {
      Contract.Requires<ArgumentNullException>(variable != null);
      staticVariables.Remove(variable.Name);

      if (variable.Type != null)
        variable_types.Remove(variable);
    }

    public void ClearGlobalVariables() {
      staticVariables.Clear();
    }

    public bool HasGlobalVariable(string name) {
      Contract.Requires(name != null);
      return staticVariables.ContainsKey(name);
    }

    public IVariable GetGlobalVariable(string name) {
      Contract.Requires(name != null);
      if (HasGlobalVariable(name))
        return staticVariables[name];
      return null;
    }

    public Type GetVariableType(NameSegment ns) {
      Contract.Requires(ns != null);
      return GetVariableType(ns.Name);
    }

    public Type GetVariableType(string name) {
      Contract.Requires(name != null);
      if (TacnyOptions.O.EvalAnalysis)
        return variable_types.FirstOrDefault(i => i.Key.Name == name).Value;

      return null;
    }
  }
  #endregion
}
