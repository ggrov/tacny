using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dafny;

namespace Dare
{
    internal class RemovableTypeFinder
    {
        private Program Program { get; set; }
        readonly AllRemovableTypes _allRemovableTypes = new AllRemovableTypes();
        //needed to identify scope of methods for lemma calls
        private readonly Dictionary<ModuleDefinition, Dictionary<ClassDecl, List<Method>>> _allMethods = new Dictionary<ModuleDefinition, Dictionary<ClassDecl, List<Method>>>();


        public RemovableTypeFinder(Program program)
        {
            Program = program;
        }

        public AllRemovableTypes FindRemovables()
        {
            //First we need to find all the methods so the lemma calls can find them
            IdentifyModule(Program.DefaultModuleDef);
            //Now we check each module to find the removables
            FindRemovableTypesInModule(Program.DefaultModuleDef);
            return _allRemovableTypes;
        }

        public RemovableTypesInMember FindRemovableTypesInSingleMember(MemberDecl member)
        {
            IdentifyModule(Program.DefaultModuleDef);
            ClassDecl classDecl = FindClassOfMember(member);
            FindRemovableTypesInMember(member, classDecl);
            return _allRemovableTypes.RemovableTypesInMethods[member];
        }

        private ClassDecl FindClassOfMember(MemberDecl memberDecl)
        {
            foreach (var classDict in _allMethods.Values) {
                foreach (var classDecl in classDict.Keys) {
                    if(classDict[classDecl].Contains(memberDecl))
                        return classDecl;
                }
            }
            throw new Exception("Failed to find class!");
        }

        private void IdentifyModule(ModuleDefinition module)
        {
            if (_allMethods.ContainsKey(module)) return;
            _allMethods.Add(module, new Dictionary<ClassDecl, List<Method>>());
            foreach (var decl in module.TopLevelDecls)
                IdentifyTopLevelDecl(decl);
        }

        private void IdentifyTopLevelDecl(Declaration decl)
        {
            if (decl is ClassDecl)
                IdentifyClass((ClassDecl) decl);
            else if (decl is LiteralModuleDecl) {
                var literalModule = (LiteralModuleDecl) decl;
                IdentifyModule(literalModule.ModuleDef);
            }
        }

        private void IdentifyClass(ClassDecl classDecl)
        {
            _allMethods[classDecl.Module].Add(classDecl, new List<Method>());
            foreach (var member in classDecl.Members)
                if (member is Method) {
                    _allMethods[classDecl.Module][classDecl].Add((Method) member);
                    _allRemovableTypes.AddMember(member);
                }
        }

        private void FindRemovableTypesInModule(ModuleDefinition module)
        {
            foreach (var decl in module.TopLevelDecls) {
                if (decl is ClassDecl)
                    FindRemovableTypesInClass((ClassDecl) decl);
                else if (decl is LiteralModuleDecl) {
                    var literalModule = (LiteralModuleDecl) decl;
                    FindRemovableTypesInModule(literalModule.ModuleDef);
                }
            }
        }

        private void FindRemovableTypesInClass(ClassDecl classDecl)
        {
            foreach (var member in classDecl.Members) {
                FindRemovableTypesInMember(member, classDecl);
            }
        }

        private void FindRemovableTypesInMember(MemberDecl member, ClassDecl classDecl)
        {
            if (member is Tactic) return;
            WildCardDecreases wildCardParent = null; // The parent of the current wildCard we are tracking
            FindDecreasesTypesInMember(member, ref wildCardParent);
            var method = member as Method;
            if (method != null)
                FindRemovableTypesInMethod(method, wildCardParent, classDecl);
        }

        private void FindDecreasesTypesInMember(MemberDecl member, ref WildCardDecreases wildCardParent)
        {
            Specification<Expression> decreases = null;
            if (member is Method) {
                var method = (Method) member;
                decreases = method.Decreases;
            }
            else if (member is Function) {
                var function = (Function) member;
                decreases = function.Decreases;
            }
            if (decreases != null)
                FindDecreasesTypesInMethodFunction(decreases, ref wildCardParent, member);
        }

        private void FindDecreasesTypesInMethodFunction(Specification<Expression> decreases, ref WildCardDecreases wildCardParent, MemberDecl member)
        {
            foreach (var expression in decreases.Expressions) {
                if (expression is WildcardExpr) {
                    wildCardParent = new WildCardDecreases(expression, decreases, null);
                    _allRemovableTypes.AddWildCardDecreases(wildCardParent, (Method) member);
                    continue;
                }
                _allRemovableTypes.AddDecreases(new Wrap<Expression>(expression, decreases.Expressions), member);
            }
        }

        private void FindRemovableTypesInMethod(Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            if (method.Body == null) return;
            var block = method.Body;
            foreach (var statement in block.Body)
                FindRemovableTypesInStatement(statement, block, method, wildCardParent, classDecl);
        }

        private void FindRemovableTypesInStatement(Statement statement, Statement parent, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            if (statement is AssertStmt)
                FindRemovableTypesInAssertStmt((AssertStmt) statement, parent, method);
            else if (statement is BlockStmt)
                FindRemovableTypesInBlockStmt((BlockStmt) statement, method, wildCardParent, classDecl);
            else if (statement is IfStmt)
                FindRemovableTypesInIfStmt((IfStmt) statement, method, wildCardParent, classDecl);
            else if (statement is LoopStmt)
                FindRemovableTypesInLoopStmt((LoopStmt) statement, method, wildCardParent, classDecl);
            else if (statement is MatchStmt)
                FindRemovableTypesInMatchStmt((MatchStmt) statement, method, wildCardParent, classDecl);
            else if (statement is ForallStmt)
                FindRemovableTypesInForallStmt((ForallStmt) statement, method, wildCardParent, classDecl);
            else if (statement is CalcStmt)
                FindRemovableTypesInCalcStmt((CalcStmt) statement, parent, method, wildCardParent, classDecl);
            else if (statement is UpdateStmt)
                FindRemovableTypesInUpdateStmt((UpdateStmt) statement, parent, method, classDecl);
        }

        private void FindRemovableTypesInCalcStmt(CalcStmt calc, Statement parent, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            Wrap<Statement> calcWrap = null;
            if (parent is BlockStmt)
                calcWrap = new Wrap<Statement>(calc, ((BlockStmt)parent).Body);
            else if (parent is MatchStmt) {
                var matchStmt = (MatchStmt) parent;
                foreach (var matchCase in matchStmt.Cases) {
                    if (!matchCase.Body.Contains(calc)) continue;
                    calcWrap = new Wrap<Statement>(calc, matchCase.Body);
                    break;
                }
                if (calcWrap == null) throw  new Exception("Calc not found!");
            }
            else {
                throw new Exception("Calc not found!");
            }

            _allRemovableTypes.AddCalc(calcWrap, method);
            foreach (var hint in calc.Hints) {
                FindRemovableTypesInStatement(hint, calc, method, wildCardParent, classDecl); // This will check the inside of the hint - it will ID anything that can be shortened inside it.
            }

        }
        private void FindRemovableTypesInForallStmt(ForallStmt forall, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            FindRemovableTypesInStatement(forall.Body, forall, method, wildCardParent, classDecl);
        }

        private void FindRemovableTypesInMatchStmt(MatchStmt match, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            foreach (var matchCase in match.Cases)
                foreach (var stmt in matchCase.Body)
                    FindRemovableTypesInStatement(stmt, match, method, wildCardParent, classDecl);
        }

        private void FindRemovableTypesInLoopStmt(LoopStmt loopStmt, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            GetLoopInvariants(loopStmt, method);
            IdentifyRemovableDecreasesTypesInLoop(loopStmt, method, ref wildCardParent);
            if (!(loopStmt is WhileStmt)) return;
            var whileStmt = (WhileStmt) loopStmt;
            FindRemovableTypesInStatement(whileStmt.Body, loopStmt, method, wildCardParent, classDecl);
        }

        private void IdentifyRemovableDecreasesTypesInLoop(LoopStmt loop, Method method, ref WildCardDecreases wildCardParent)
        {
            foreach (var expr in loop.Decreases.Expressions) {
                IdentifyDecreasesExpression(loop, method, ref wildCardParent, expr);
            }
        }

        private void IdentifyDecreasesExpression(LoopStmt loop, Method method, ref WildCardDecreases wildCardParent, Expression expr)
        {
            if (expr is WildcardExpr)
                IdentifyWildCardDecreases(loop, ref wildCardParent, expr);
            else
                _allRemovableTypes.AddDecreases(new Wrap<Expression>(expr, loop.Decreases.Expressions), method);
        }

        private void IdentifyWildCardDecreases(LoopStmt loop, ref WildCardDecreases wildCardParent, Expression expr)
        {
            var newWildCard = new WildCardDecreases(expr, loop.Decreases, wildCardParent);
            wildCardParent.SubDecreases.Add(newWildCard);
            wildCardParent = newWildCard;
        }

        void GetLoopInvariants(LoopStmt loop, Method method)
        {
            foreach (var invariant in loop.Invariants) {
                _allRemovableTypes.AddInvariant(new Wrap<MaybeFreeExpression>(invariant, loop.Invariants), method);
            }
        }

        private void FindRemovableTypesInIfStmt(IfStmt ifstmt, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            FindRemovableTypesInStatement(ifstmt.Thn, ifstmt, method, wildCardParent, classDecl);
            FindRemovableTypesInStatement(ifstmt.Els, ifstmt, method, wildCardParent, classDecl);
        }

        private void FindRemovableTypesInBlockStmt(BlockStmt blockStmt, Method method, WildCardDecreases wildCardParent, ClassDecl classDecl)
        {
            foreach (var stmt in blockStmt.Body)
                FindRemovableTypesInStatement(stmt, blockStmt, method, wildCardParent, classDecl);
        }

        private void FindRemovableTypesInAssertStmt(AssertStmt assert, Statement parent, Method method)
        {
            if (!(parent is BlockStmt)) return;
            var block = (BlockStmt) parent;
            var assertWrap = new Wrap<Statement>(assert, block.Body);
            _allRemovableTypes.AddAssert(assertWrap, method);
        }

        private void FindRemovableTypesInUpdateStmt(UpdateStmt updateStmt, List<Statement> parent, Method method, ClassDecl classDecl)
        {
            foreach (var expr in updateStmt.Rhss) {
                if (!IsAssignmentLemmaCall(expr, classDecl)) continue;
                _allRemovableTypes.AddLemmaCall(new Wrap<Statement>(updateStmt, parent), method);
            }
        }

        private void FindRemovableTypesInUpdateStmt(UpdateStmt updateStmt, Statement parent, Method method, ClassDecl classDecl)
        {
            if (parent is BlockStmt) {
                var blockStmt = (BlockStmt) parent;
                FindRemovableTypesInUpdateStmt(updateStmt, blockStmt.Body, method, classDecl);
            }
            else if (parent is MatchStmt) {
                var matchStmt = (MatchStmt) parent;
                foreach (var matchCase in matchStmt.Cases) {
                    if (!matchCase.Body.Contains(updateStmt)) continue;
                    FindRemovableTypesInUpdateStmt(updateStmt, matchCase.Body, method, classDecl);
                    break;
                }
            }
        }

        private bool IsAssignmentLemmaCall(AssignmentRhs expr, ClassDecl classDecl)
        {
            var exprRhs = expr as ExprRhs;
            if (exprRhs == null) return false;
            if (!(exprRhs.Expr is ApplySuffix)) return false;
            return IsCallToGhost((ApplySuffix) exprRhs.Expr, classDecl);
        }

        private bool IsCallToGhost(SuffixExpr expr, ClassDecl classDecl)
        {
            var name = "";
            var nameSeg = expr.Lhs as NameSegment;
            if (nameSeg != null)
                name = nameSeg.Name;

            // Look through all the methods within the current scope and return whether it is ghost or not
            return (from method in _allMethods[classDecl.Module][classDecl] where method.Name == name select method.IsGhost).FirstOrDefault();
        }
    }
}
