using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Util;
using Formal = Microsoft.Dafny.Formal;
using Type = Microsoft.Dafny.Type;

// todo cases multiple tac calls, cases within cases, tac calls from cases etc.
// update 

namespace LazyTacny {
  class MatchAtomic : Atomic, IAtomicLazyStmt {

    private IToken _oldToken;
    private Dictionary<string, Type> _ctorTypes;

    public MatchAtomic(Atomic atomic) : base(atomic) { }

    public IEnumerable<Solution> Resolve(Statement st, Solution solution) {
      return GenerateMatch(st as TacnyCasesBlockStmt);
    }

    /*
     * A matchStatement error token will always be a case tok 
     * we find the first failing cases block and return the index 
     */
    public int GetErrorIndex(IToken errorToken, MatchStmt st) {
      foreach (var item in st.Cases)
        if (item.tok == errorToken)
          return st.Cases.IndexOf(item);

      return -1;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="errorToken"></param>
    /// <param name="ms"></param>
    /// <param name="ctor"></param>
    /// <returns></returns>
    public bool ErrorChanged(IToken errorToken, MatchStmt ms, int ctor) {
      // check if error has been generated
      if (_oldToken == null || errorToken == null)
        return true;

      /**
       * Check if the error originates in the current cases statement
       */
      if (_oldToken.line <= ms.Cases[ctor].tok.line + ms.Cases[ctor].Body.Count || errorToken.line == ms.Cases[ctor].tok.line) {
        // if the error occurs in the last cases element
        if (ctor + 1 == ms.Cases.Count)
        {
          // check if the error resides anywhere in the last case body
          return errorToken.line > _oldToken.line && errorToken.line <= (ms.Cases[ctor].tok.line + ms.Cases[ctor].Body.Count);
        }
        return errorToken.line > _oldToken.line && errorToken.line <= ms.Cases[ctor + 1].tok.line;
      }
      // the error must have changed
      return true;
    }

    private IEnumerable<Solution> GenerateMatch(TacnyCasesBlockStmt st) {
      NameSegment casesGuard;
      UserDefinedType datatypeType = null;
      var isElement = false;

      var guard = st.Guard as ParensExpression;

      if (guard == null)
        casesGuard = st.Guard as NameSegment;
      else
        casesGuard = guard.E as NameSegment;

      Contract.Assert(casesGuard != null, Error.MkErr(st, 2));

      IVariable tacInput = GetLocalKeyByName(casesGuard);
      Contract.Assert(tacInput != null, Error.MkErr(st, 9, casesGuard.Name));


      if (!(tacInput is Formal)) {
        tacInput = GetLocalValueByName(casesGuard) as IVariable;
        Contract.Assert(tacInput != null, Error.MkErr(st, 9, casesGuard.Name));
        // the original
        if (tacInput != null) casesGuard = new NameSegment(tacInput.Tok, tacInput.Name, null);
      } else {
        // get the original declaration inside the method
        casesGuard = GetLocalValueByName(tacInput) as NameSegment;
      }
      string datatypeName = tacInput?.Type.ToString();
      /**
       * TODO cleanup
       * if datatype is Element lookup the formal in global variable registry
       */

      if (datatypeName == "Element") {
        isElement = true;
        var val = GetLocalValueByName(tacInput.Name);
        var decl = val as NameSegment;
        Contract.Assert(decl != null, Error.MkErr(st, 9, tacInput.Name));

        var originalDecl = StaticContext.GetGlobalVariable(decl?.Name);
        if (originalDecl != null)
        {
          datatypeType = originalDecl.Type as UserDefinedType;
          datatypeName = datatypeType != null ? datatypeType.Name : originalDecl.Type.ToString();
        }
        else
          Contract.Assert(false, Error.MkErr(st, 9, tacInput.Name));
      }

      if (!StaticContext.ContainsGlobalKey(datatypeName)) {
        Contract.Assert(false, Error.MkErr(st, 12, datatypeName));
      }

      var datatype = StaticContext.GetGlobal(datatypeName);

      if (datatype.TypeArgs != null) {
        _ctorTypes = new Dictionary<string, Type>();

        if (datatype.TypeArgs.Count == datatypeType.TypeArgs.Count) {
          for (int i = 0; i < datatype.TypeArgs.Count; i++) {
            var genericType = datatype.TypeArgs[i];
            var definedType = datatypeType.TypeArgs[i];
            _ctorTypes.Add(genericType.Name, definedType);

          }
        }
      }

      if (isElement) {
        yield return GenerateVerifiedStmt(datatype, casesGuard, st);
      } else {
        foreach (var item in GenerateStmt(datatype, casesGuard, st))
          yield return item;
      }
    }

    /// <summary>
    /// TODO: Resolve the bodies lazily
    /// </summary>
    /// <param name="datatype"></param>
    /// <param name="casesGuard"></param>
    /// <param name="st"></param>
    /// <returns></returns>
    private IEnumerable<Solution> GenerateStmt(DatatypeDecl datatype, NameSegment casesGuard, TacnyCasesBlockStmt st) {
      List<List<Solution>> allCtorBodies = Repeated(new List<Solution>(), datatype.Ctors.Count);
      int ctor = 0;
      
      foreach (var list in allCtorBodies) {
        list.Add(null);

        RegisterLocals(datatype, ctor);
        foreach (var result in ResolveBody(st.Body)) {
          list.Add(result);
        }

        RemoveLocals(datatype, ctor);
        ctor++;
      }

      foreach (var stmt in GenerateAllMatchStmt(DynamicContext.tac_call.Tok.line, 0, Util.Copy.CopyNameSegment(casesGuard), datatype, allCtorBodies, new List<Solution>())) {
        yield return CreateSolution(this, stmt);
      }
    }


    private Solution GenerateVerifiedStmt(DatatypeDecl datatype, NameSegment casesGuard, TacnyCasesBlockStmt st) {
      bool[] ctorFlags;
      int ctor; // current active match case
      InitCtorFlags(datatype, out ctorFlags);
      List<Solution> ctorBodies = RepeatedDefault<Solution>(datatype.Ctors.Count);
      // find the first failing case 
      MatchStmt ms = GenerateMatchStmt(DynamicContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(casesGuard), datatype, ctorBodies);
      Solution solution = CreateSolution(this, ms);
      if (!ResolveAndVerify(solution)) {
        ctor = 0;
      } else {
        ctor = GetErrorIndex(StaticContext.program.GetErrorToken(), ms);
        // the error is occuring outside the match stmt
        if (ctor == -1) {
          ms = GenerateMatchStmt(DynamicContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(casesGuard), datatype, ctorBodies);
          return CreateSolution(this, ms);
        }
        ctorFlags[ctor] = true;
        _oldToken = StaticContext.program.GetErrorToken();
      }
      while (ctor < datatype.Ctors.Count) {

        if (!StaticContext.program.HasError())
          break;
        RegisterLocals(datatype, ctor, _ctorTypes);

        // if nothing was generated for the cases body move on to the next one
        foreach (var result in ResolveBody(st.Body)) {

          ctorBodies[ctor] = result;
          ms = GenerateMatchStmt(DynamicContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(casesGuard), datatype, ctorBodies);
          solution = CreateSolution(this, ms);
          // if the program fails tro resolve skip
          if (!ResolveAndVerify(solution))
            continue;

          if (!StaticContext.program.HasError())
            break;
          if (CheckError(ms, ref ctorFlags, ctor)) {
            // if the ctor does not require a body null the value
            if (!ctorFlags[ctor])
              ctorBodies[ctor] = null;
            break;
          }
        }
        // clear local var 
        RemoveLocals(datatype, ctor);
        ctor++;

      }

      ms = GenerateMatchStmt(DynamicContext.tac_call.Tok.line, Util.Copy.CopyNameSegment(casesGuard), datatype, ctorBodies);
      return CreateSolution(this, ms);
    }

    private MatchStmt GenerateMatchStmt(int index, NameSegment ns, DatatypeDecl datatype, List<Solution> body) {
      Contract.Requires(ns != null);
      Contract.Requires(datatype != null);
      Contract.Ensures(Contract.Result<MatchStmt>() != null);
      List<MatchCaseStmt> cases = new List<MatchCaseStmt>();

      int line = index + 1;
      int i = 0;
      foreach (DatatypeCtor dc in datatype.Ctors) {
        MatchCaseStmt mcs;
        GenerateMatchCaseStmt(line, dc, body[i], out mcs);

        cases.Add(mcs);
        line += mcs.Body.Count + 1;
        i++;
      }

      return new MatchStmt(CreateToken("match", index, 0), CreateToken("=>", index, 0), ns, cases, false);
    }

    private IEnumerable<MatchStmt> GenerateAllMatchStmt(int line_index, int depth, NameSegment ns, DatatypeDecl datatype, List<List<Solution>> bodies, List<Solution> curBody) {
      if (bodies.Count == 0) yield break;
      if (depth == bodies.Count) {
        MatchStmt ms = GenerateMatchStmt(line_index, Util.Copy.CopyNameSegment(ns), datatype, curBody);
        yield return ms;
        yield break;

      }
      for (int i = 0; i < bodies[depth].Count; ++i) {
        List<Solution> tmp = new List<Solution>();
        tmp.AddRange(curBody);
        tmp.Add(bodies[depth][i]);
        foreach (var item in GenerateAllMatchStmt(line_index, depth + 1, ns, datatype, bodies, tmp))
          yield return item;
      }
    }

    private void GenerateMatchCaseStmt(int line, DatatypeCtor dtc, Solution solution, out MatchCaseStmt mcs) {
      Contract.Requires(dtc != null);
      Contract.Ensures(Contract.ValueAtReturn(out mcs) != null);
      List<CasePattern> casePatterns = new List<CasePattern>();
      dtc = new DatatypeCtor(dtc.tok, dtc.Name, dtc.Formals, dtc.Attributes);
      foreach (var formal in dtc.Formals) {
        CasePattern cp;
        GenerateCasePattern(line, formal, out cp);
        casePatterns.Add(cp);
      }

      List<Statement> body = new List<Statement>();
      if (solution != null) {
        Atomic ac = solution.State.Copy();
        body = ac.GetAllUpdated();
      }
      mcs = new MatchCaseStmt(CreateToken("cases", line, 0), dtc.CompileName, casePatterns, body);
    }

    private void GenerateCasePattern(int line, Formal formal, out CasePattern cp) {
      Contract.Requires(formal != null);
      formal = new Formal(formal.tok, formal.Name, formal.Type, formal.InParam, formal.IsGhost);

      cp = new CasePattern(CreateToken(formal.Name, line, 0),
                              new BoundVar(CreateToken(formal.Name, line, 0), formal.Name, new InferredTypeProxy()));
    }

    private static void InitCtorFlags(DatatypeDecl datatype, out bool[] flags, bool value = false) {
      flags = new bool[datatype.Ctors.Count];
      for (int i = 0; i < flags.Length; i++) {
        flags[i] = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private bool CheckError(MatchStmt ms, ref bool[] ctorFlags, int ctor) {
      // hack for termination
      if (StaticContext.program.ErrorInfo.Msg == "cannot prove termination; try supplying a decreases clause")
        return false;
      // if the error token has not changed since last iteration
      if (!ErrorChanged(StaticContext.program.GetErrorToken(), ms, ctor))
        return false;

      _oldToken = StaticContext.program.GetErrorToken();
      if (_oldToken != null) {
        int index = GetErrorIndex(_oldToken, ms);
        // the verification error is not caused by the match stmt
        if (index == -1)
          return false;
        ctorFlags[index] = true;
        return true;
      }
      return false;
    }

    public static List<T> RepeatedDefault<T>(int count) {
      return Repeated(default(T), count);
    }


    public static List<T> Repeated<T>(T value, int count) {
      List<T> ret = new List<T>(count);
      ret.AddRange(Enumerable.Repeat(value, count));
      return ret;
    }

    public static Solution CreateSolution(Atomic atomic, MatchStmt ms) {
      Atomic ac = atomic.Copy();
      ac.AddUpdated(ms, ms);
      return new Solution(ac, true, null);
    }
  }
}
