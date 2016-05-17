using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using System.Diagnostics;
namespace LazyTacny {
  public class SolutionList {

    public List<Solution> plist;

    private List<List<Solution>> final; // a list of solutions ofr each tactic

    public SolutionList() {
      plist = new List<Solution>();
      final = new List<List<Solution>>();
    }

    public SolutionList(Solution solution) {
      Contract.Requires(solution != null);
      plist = new List<Solution>() { solution };
      final = new List<List<Solution>>();
    }

    public void Add(Solution solution) {
      plist.Add(solution);
    }

    public void AddRange(List<Solution> solutions) {
      // remove non final solutions
      List<Solution> tmp = new List<Solution>();
      foreach (var item in plist) {
        if (item.state.DynamicContext.IsResolved())
          tmp.Add(item);
      }
      plist.Clear();
      plist = tmp;
      //plist.Clear();
      plist.AddRange(solutions);
    }

    public void AddFinal(List<Solution> solutions) {
      final.Add(new List<Solution>(solutions.ToArray()));
    }

    public bool IsFinal() {
      foreach (var item in plist)
        if (!item.isFinal)
          return false;
      return true;
    }

    public void SetIsFinal() {
      foreach (var item in plist)
        item.isFinal = true;
    }

    public void UnsetFinal() {
      foreach (var item in plist)
        item.isFinal = false;
    }

    public List<List<Solution>> GetFinal() {
      return final;
    }

    public void Fin() {
      if (plist.Count > 0) {
        AddFinal(plist);
        plist.Clear();
      }

    }
  }


  public class Solution {
    public Atomic state;
    private Solution _parent = null;
    public Solution parent {
      set { _parent = value; }
      get { return _parent; }
    }

    public bool isFinal = false;

    public Solution(Atomic state, Solution parent = null)
        : this(state, false, parent) { }

    public Solution(Atomic state, bool isFinal, Solution parent) {
      this.state = state;
      this.isFinal = isFinal;
      this.parent = parent;
    }

    [Pure]
    public bool IsResolved() {
      return state.DynamicContext.IsResolved();
    }


    public Program GenerateProgram(Function func, Program prog) {
      ClassDecl curDecl;
      for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++) {
        curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
        if (curDecl != null) {
          // scan each member for tactic calls and resolve if found
          for (int j = 0; j < curDecl.Members.Count; j++) {
            if (curDecl.Members[i].Name == func.Name)
              curDecl.Members[i] = func;
          }

          prog.DefaultModuleDef.TopLevelDecls[i] = Tacny.Program.RemoveTactics(curDecl);
        }
      }

      return prog;
    }

    public string GenerateProgram(ref Dafny.Program prog, bool isFinal = false) {
      Debug.WriteLine("Generating Dafny program");
      Method method = null;
      List<Dafny.Program> prog_list = new List<Dafny.Program>();
      Atomic ac = state.Copy();
      MemberDecl newMemberDecl = null;
      ac.Fin();
      if (!ac.IsFunction) {
        method = Tacny.Program.FindMember(prog, ac.DynamicContext.md.Name) as Method;
        if (method == null)
          throw new Exception("Method not found");
        UpdateStmt tac_call = ac.GetTacticCall();
        List<Statement> body = method.Body.Body;
        body = InsertSolution(body, tac_call, ac.GetResolved());
        if (body == null)
          return null;
        if (!isFinal) {
          for (int i = 0; i < body.Count; i++) {
            if (body[i] is UpdateStmt) {
              if (state.StaticContext.program.IsTacticCall(body[i] as UpdateStmt))
                body.RemoveAt(i);
            }
          }
        }

        newMemberDecl = GenerateMethod(method, body, ac.DynamicContext.newTarget as Method);
      } else {
        newMemberDecl = ac.GetNewTarget();
      }
      ClassDecl curDecl;
      for (int i = 0; i < prog.DefaultModuleDef.TopLevelDecls.Count; i++) {
        curDecl = prog.DefaultModuleDef.TopLevelDecls[i] as ClassDecl;
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

    private static Method GenerateMethod(Method oldMd, List<Statement> body, Method source = null) {
      Method src = source == null ? oldMd : source;
      BlockStmt mdBody = new BlockStmt(src.Body.Tok, src.Body.EndTok, body);
      System.Type type = src.GetType();
      if (type == typeof(Lemma))
        return new Lemma(src.tok, src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
        src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
      else if (type == typeof(CoLemma))
        return new CoLemma(src.tok, src.Name, src.HasStaticKeyword, src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod,
        src.Ens, src.Decreases, mdBody, src.Attributes, src.SignatureEllipsis);
      else
        return new Method(src.tok, src.Name, src.HasStaticKeyword, src.IsGhost,
            src.TypeArgs, src.Ins, src.Outs, src.Req, src.Mod, src.Ens, src.Decreases,
            mdBody, src.Attributes, src.SignatureEllipsis);
    }

    public static void PrintSolution(Solution solution) {
      Dafny.Program prog = solution.state.StaticContext.program.ParseProgram();
      solution.GenerateProgram(ref prog);
      solution.state.StaticContext.program.ClearBody(solution.state.DynamicContext.md);
      Console.WriteLine(String.Format("Tactic call {0} in {1} results: ", solution.state.DynamicContext.tactic.Name, solution.state.DynamicContext.md.Name));
      solution.state.StaticContext.program.PrintMember(prog, solution.state.StaticContext.md.Name);
    }

    private static List<Statement> InsertSolution(List<Statement> body, UpdateStmt tac_call, List<Statement> solution) {
      int index = FindTacCall(body, tac_call);
      if (index == -1)
        return null;

      var newBody = new List<Statement>();
      var tmp = body.ToArray();
      newBody = new List<Statement>(tmp);
      newBody.RemoveAt(index);
      newBody.InsertRange(index, solution);


      return newBody;
    }

    private static int FindTacCall(List<Statement> body, UpdateStmt tac_call) {
      for (int j = 0; j < body.Count; j++) {
        
        if (body[j] is UpdateStmt) {
          var us = body[j] as UpdateStmt;
          if (CompareUpdateStmt(us, tac_call))
            return j;
        } else if (body[j] is WhileStmt) {
          var us = ((WhileStmt)body[j]).TacAps as UpdateStmt;
          if (CompareUpdateStmt(us, tac_call))
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

    private static TopLevelDecl ExtractContext(MemberDecl md, ClassDecl cd) {
      Dictionary<string, MemberDecl> context = new Dictionary<string, MemberDecl>();
      if (md is Method) {
        Method method = Util.Copy.CopyMethod(md as Method);
        // check what member declarations are called in the pre condition
        List<MaybeFreeExpression> lMfe = method.Req;
        foreach (var mfe in lMfe) {
          Expression exp = mfe.E;
        }
      }
      return null;
    }
  }
}
