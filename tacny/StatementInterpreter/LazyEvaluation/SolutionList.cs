using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;

namespace LazyTacny {
  public class SolutionList {

    public List<Solution> Plist;

    private List<List<Solution>> _final; // a list of solutions ofr each tactic

    public SolutionList() {
      Plist = new List<Solution>();
      _final = new List<List<Solution>>();
    }

    public SolutionList(Solution solution) {
      Contract.Requires(solution != null);
      Plist = new List<Solution> { solution };
      _final = new List<List<Solution>>();
    }

    public void Add(Solution solution) {
      Plist.Add(solution);
    }

    public void AddRange(List<Solution> solutions) {
      // remove non final solutions
      List<Solution> tmp = new List<Solution>();
      foreach (var item in Plist) {
        if (item.State.DynamicContext.IsResolved())
          tmp.Add(item);
      }
      Plist.Clear();
      Plist = tmp;
      //plist.Clear();
      Plist.AddRange(solutions);
    }

    public void AddFinal(List<Solution> solutions) {
      _final.Add(new List<Solution>(solutions.ToArray()));
    }

    public bool IsFinal() {
      foreach (var item in Plist)
        if (!item.IsFinal)
          return false;
      return true;
    }

    public void SetIsFinal() {
      foreach (var item in Plist)
        item.IsFinal = true;
    }

    public void UnsetFinal() {
      foreach (var item in Plist)
        item.IsFinal = false;
    }

    public List<List<Solution>> GetFinal() {
      return _final;
    }

    public void Fin() {
      if (Plist.Count > 0) {
        AddFinal(Plist);
        Plist.Clear();
      }

    }
  }


  public class Solution {
    public Atomic State;
    public Solution Parent { set; get; }

    public bool IsFinal;

    public Solution(Atomic state, Solution parent = null)
        : this(state, false, parent) { }

    public Solution(Atomic state, bool isFinal, Solution parent) {
      State = state;
      IsFinal = isFinal;
      Parent = parent;
    }

    [Pure]
    public bool IsResolved() {
      return State.DynamicContext.IsResolved();
    }


    public Program GenerateProgram(Function func, Program prog) {
      for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++)
      {
        var curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
        if (curDecl == null) continue;
        // scan each member for tactic calls and resolve if found
        for (int j = 0; j < curDecl.Members.Count; j++) {
          if (curDecl.Members[i].Name == func.Name)
            curDecl.Members[i] = func;
        }

        prog.DefaultModuleDef.TopLevelDecls[i] = Tacny.Program.RemoveTactics(curDecl);
      }

      return prog;
    }
    

    public string GenerateProgram(ref Program prog, bool isFinal = false) {
      Debug.WriteLine("Generating Dafny program");
      var ac = State.Copy();
      MemberDecl newMemberDecl;
      ac.Fin();
      if (!ac.IsFunction) {
        var method = Tacny.Program.FindMember(prog, ac.DynamicContext.md.Name) as Method;
        if (method == null)
          throw new Exception("Method not found");
        UpdateStmt tacCall = ac.GetTacticCall();
        List<Statement> body = method.Body.Body;
        body = InsertSolution(body, tacCall, ac.GetResolved());
        if (body == null)
          return null;
        if (!isFinal) {
          for (int i = 0; i < body.Count; i++)
          {
            var us = body[i] as UpdateStmt;
            if (us == null) continue;
            if (State.StaticContext.program.IsTacticCall(us))
              body.RemoveAt(i);
          }
        }

        newMemberDecl = GenerateMethod(method, body, ac.DynamicContext.newTarget as Method);
      } else {
        newMemberDecl = ac.GetNewTarget();
      }
      for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++)
      {
        var curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
        if (curDecl != null) {
          // scan each member for tactic calls and resolve if found
          for (int j = 0; j < curDecl.Members.Count; j++) {


            if (curDecl.Members[j].Name == newMemberDecl.Name)
              curDecl.Members[j] = newMemberDecl;
          }

          prog.DefaultModuleDef.TopLevelDecls[i] = Tacny.Program.RemoveTactics(curDecl);
        }
      }

      Debug.WriteLine("Dafny program generated");
      return null;
    }


    public static Method GenerateMethod(Method oldMd, List<Statement> body, Method source = null, string newName = null) {
      var src = source ?? oldMd;
      var mdBody = new BlockStmt(src.Body.Tok, src.Body.EndTok, body);
      var type = src.GetType();
      if (type == typeof(Lemma))
        return new Lemma(src.tok, newName ?? src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
        src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
      if (type == typeof(CoLemma))
        return new CoLemma(src.tok, newName ?? src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
          src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
      return new Method(src.tok, newName ?? src.Name, src.HasStaticKeyword, src.IsGhost,
        src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod, src.Ens, src.Decreases,
        mdBody, src.Attributes, src.SignatureEllipsis);
    }

    public static void PrintSolution(Solution solution) {
      var prog = solution?.State.StaticContext.program.ParseProgram();
      solution?.GenerateProgram(ref prog);
      solution?.State.StaticContext.program.ClearBody(solution.State.DynamicContext.md);
      Console.WriteLine(
        $"Tactic call {solution?.State.DynamicContext.tactic.Name} in {solution?.State.DynamicContext.md.Name} results: ");
      solution?.State.StaticContext.program.PrintMember(prog, solution.State.StaticContext.md.Name);
    }

    public static List<Statement> InsertSolution(List<Statement> body, UpdateStmt tacCall, List<Statement> solution) {
      int index = FindTacCall(body, tacCall);
      if (index == -1)
        return null;

      var tmp = body.ToArray();
      var newBody = new List<Statement>(tmp);
      newBody.RemoveAt(index);
      newBody.InsertRange(index, solution);


      return newBody;
    }

    private static int FindTacCall(List<Statement> body, UpdateStmt tacCall) {
      for (int j = 0; j < body.Count; j++)
      {
        var stmt = body[j] as UpdateStmt;
        if (stmt != null) {
          var us = stmt;
          if (CompareUpdateStmt(us, tacCall))
            return j;
        } else if (body[j] is WhileStmt) {
          var us = ((WhileStmt)body[j]).TacAps as UpdateStmt;
          if (CompareUpdateStmt(us, tacCall))
            return j;
        }
      }
      return -1;
    }

    private static bool CompareUpdateStmt(UpdateStmt st1, UpdateStmt st2) {
      if (st1 == null || st2 == null)
        return false;
      return st1.Tok.line == st2.Tok.line && st1.Tok.col == st2.Tok.col;
    }

/*
    private static TopLevelDecl ExtractContext(MemberDecl md, ClassDecl cd) {
      Dictionary<string, MemberDecl> context = new Dictionary<string, MemberDecl>();
      if (md is Method) {
        Method method = Copy.CopyMethod(md as Method);
        // check what member declarations are called in the pre condition
        List<MaybeFreeExpression> lMfe = method.Req;
        foreach (var mfe in lMfe) {
          Expression exp = mfe.E;
        }
      }
      return null;
    }
*/
  }
}
