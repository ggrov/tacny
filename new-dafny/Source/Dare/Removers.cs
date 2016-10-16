using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Program = Microsoft.Dafny.Program;

namespace Dare
{
    public class SimpleVerifier
    {
        public void BoogieErrorInformation(ErrorInformation errorInfo) {}

        public bool IsProgramValid(Program program)
        {
            return IsProgramValid(program, null);
        }

        public bool IsProgramValid(Program program, ErrorReporterDelegate errorDelegate)
        {
            try {
                return TryValidateProgram(program, errorDelegate);
            }
            catch (FileNotFoundException e) {
                throw e;
            }
            catch (Exception) {
                return false;
            }
        }

        public static bool TryValidateProgram(Program program, ErrorReporterDelegate errorDelegate)
        {
            var translator = new Translator(new InvisibleErrorReporter());
            var programCopy = SimpleCloner.CloneProgram(program);
            var resolver = new Resolver(programCopy);
            resolver.ResolveProgram(programCopy);
            var boogieProgram = translator.Translate(programCopy);
            var stats = new PipelineStatistics();
            var programId = "main_program_id";
            var bplFileName = "bplFile";
            LinearTypeChecker ltc;
            CivlTypeChecker ctc;

            var oc = ExecutionEngine.ResolveAndTypecheck(boogieProgram, bplFileName, out ltc, out ctc);
            if (oc != PipelineOutcome.ResolvedAndTypeChecked) return false;
            ExecutionEngine.EliminateDeadVariables(boogieProgram);
            ExecutionEngine.CollectModSets(boogieProgram);
            ExecutionEngine.CoalesceBlocks(boogieProgram);
            ExecutionEngine.Inline(boogieProgram);

            oc = ExecutionEngine.InferAndVerify(boogieProgram, stats, programId, errorDelegate);
            var allOk = stats.ErrorCount == 0 && stats.InconclusiveCount == 0 && stats.TimeoutCount == 0 &&
                        stats.OutOfMemoryCount == 0;
            return oc == PipelineOutcome.VerificationCompleted && allOk;
        }
    }

    public class RemovableTypesInMember
    {
        public MemberDecl Member { get; private set; }
        public readonly List<Wrap<Statement>> Asserts = new List<Wrap<Statement>>();
        public readonly List<Wrap<MaybeFreeExpression>> Invariants = new List<Wrap<MaybeFreeExpression>>();
        public readonly List<Wrap<Expression>> Decreases = new List<Wrap<Expression>>();
        public readonly List<WildCardDecreases> WildCardDecreaseses = new List<WildCardDecreases>();
        public readonly List<Wrap<Statement>> LemmaCalls = new List<Wrap<Statement>>();
        public readonly List<Wrap<Statement>> Calcs = new List<Wrap<Statement>>();

        public RemovableTypesInMember(MemberDecl member)
        {
            Member = member;
        }
    }

    public class Wrap<T>
    {
        public T Removable { get; protected set; }
        public List<T> ParentList { get; private set; }
        public int Index { get; private set; }
        public bool Removed;

        public Wrap(T removable, List<T> parentList)
        {
            if (!parentList.Contains(removable)) throw new Exception("Removeable item is not in its parent");
            Removable = removable;
            ParentList = parentList;
            Removed = false;
            Index = -1;
        }

        public Wrap(T removable, List<T> parentList, int index)
        {
            Removable = removable;
            ParentList = parentList;
            Removed = !ParentList.Contains(Removable);
            if (!Removed)
                Index = -1;
            else if (index < 0)
                throw new Exception("Index is less than 0 on creating a removed wrap");
            else
                Index = index;
        }

        public void Remove()
        {
            if (Removed) return;
            Index = ParentList.IndexOf(Removable);
            if (Index < 0) throw new Exception("Removable item is not in its ParentList");
            ParentList.Remove(Removable);
            Removed = true;
        }

        public void Reinsert()
        {
            if (!Removed) return;
            ParentList.Insert(Index, Removable);
            Index = -1;
            Removed = false;
        }

        public static List<T> GetRemovables(List<Wrap<T>> wrapList)
        {
            return wrapList.Select(wrap => wrap.Removable).ToList();
        }
    }

    public class WildCardDecreases
    {
        public readonly Expression Expression;
        public Wrap<Expression> ExpressionWrap;
        public readonly Specification<Expression> ParentSpecification;
        public readonly WildCardDecreases ParentWildCardDecreases;
        public readonly List<WildCardDecreases> SubDecreases = new List<WildCardDecreases>();
        public bool Simplified;
        public bool CantBeRemoved;

        public int Count {
            get { return 1 + SubDecreases.Sum(wildCardDecreases => wildCardDecreases.Count); }
        }

        public WildCardDecreases(Expression decreasesExpression, Specification<Expression> parentSpecification,
            WildCardDecreases parentWildCardDecreases)
        {
            Expression = decreasesExpression;
            ParentSpecification = parentSpecification;
            ParentWildCardDecreases = parentWildCardDecreases;
            ExpressionWrap = new Wrap<Expression>(decreasesExpression, parentSpecification.Expressions);
        }
    }

    internal class CalcRemover
    {
        private readonly Program _program;
        private readonly SimpleVerifier _verifier = new SimpleVerifier();

        private List<Expression> _removableLines = new List<Expression>();
        private List<BlockStmt> _removableHints = new List<BlockStmt>();
        private List<CalcStmt.CalcOp> _removableOps = new List<CalcStmt.CalcOp>();
        private List<CalcStmt> _removableCalcStmts = new List<CalcStmt>();

        public CalcRemover(Program program)
        {
            _program = program;
        }

        public Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>> Remove(
            Dictionary<MemberDecl, List<Wrap<Statement>>> memberWrapDictionary)
        {
            foreach (var calcList in memberWrapDictionary.Values)
                RemoveCalcsInMethod(calcList);
            var removableCalcs =
                new Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>>(_removableLines,
                    _removableHints, _removableOps, _removableCalcStmts);
            Reset();
            return removableCalcs;
        }

        private void RemoveCalcsInMethod(List<Wrap<Statement>> calcList)
        {
            foreach (var calcWrap in calcList)
                RemoveCalc(calcWrap);
        }

        private void RemoveCalc(Wrap<Statement> calcWrap)
        {
            var remover = new OneAtATimeRemover(_program);
            if (remover.TryRemove(calcWrap)) {
                _removableCalcStmts.Add((CalcStmt) calcWrap.Removable);
                return;
            }
            SimplifyCalc((CalcStmt) calcWrap.Removable);
        }

        public Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>> SimplifyCalc(CalcStmt calc)
        {
            RemoveLinesAndHints(calc);
            RemoveOtherHints(calc);
            return new Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>>(_removableLines, _removableHints,
                _removableOps);
        }

        private void RemoveOtherHints(CalcStmt calc)
        {
            foreach (var hint in calc.Hints)
                CleanOutHint(hint);
        }

        private void CleanOutHint(BlockStmt hint)
        {
            if (hint.Body.Count == 0) return;
            var body = new List<Statement>();
            CloneTo(hint.Body, body);
            // empty the body - have to do it this way as it is readonly
            for (var i = 0; i < hint.Body.Count; i++) {
                var item = hint.Body[i];
                hint.Body.Remove(item);
            }
            if (_verifier.IsProgramValid(_program))
                _removableHints.Add(hint);
            else
                CloneTo(body, hint.Body);
        }

        private void RemoveLinesAndHints(CalcStmt calc)
        {
            // -2 as We don't want to touch the last two lines(dummy and last item)
            for (var i = 1; i < calc.Lines.Count - 2; i++) {
                var line = calc.Lines[i];
                for (var j = 0; j < calc.Hints.Count; j++) {
                    var hint = calc.Hints[j];
                    var stepOp = calc.StepOps[j];
                    if (
                        !TryRemove(new Wrap<Expression>(line, calc.Lines), new Wrap<BlockStmt>(hint, calc.Hints),
                            new Wrap<CalcStmt.CalcOp>(stepOp, calc.StepOps))) continue;
                    //Have to go back one as a line has been removed
                    i--;
                    _removableLines.Add(line);
                    _removableOps.Add(stepOp);
                    //Don't need to return hints that are already "invisible"
                    if (hint.Body.Count > 0)
                        _removableHints.Add(hint);
                    break;
                }
            }
        }

        public static void CloneTo(List<Statement> listToClone, List<Statement> listToCloneInto)
        {
            Contract.Requires(listToClone != null);
            //Clear out list
            foreach (var item in listToCloneInto) {
                listToCloneInto.Remove(item);
            }
            foreach (var item in listToClone) {
                listToCloneInto.Add(item);
            }
        }

        public bool TryRemove(Wrap<Expression> line, Wrap<BlockStmt> hint, Wrap<CalcStmt.CalcOp> op)
        {
            var lineIndex = line.ParentList.IndexOf(line.Removable);
            var hintIndex = hint.ParentList.IndexOf(hint.Removable);
            var opIndex = op.ParentList.IndexOf(op.Removable);
            // We should also never be trying to remove the first or last line
            Contract.Assert(lineIndex != 0);
            Contract.Assert(lineIndex != line.ParentList.Count - 1);
            line.ParentList.Remove(line.Removable);
            hint.ParentList.Remove(hint.Removable);
            op.ParentList.Remove(op.Removable);
            if (_verifier.IsProgramValid(_program)) return true;
            line.ParentList.Insert(lineIndex, line.Removable);
            hint.ParentList.Insert(hintIndex, hint.Removable);
            op.ParentList.Insert(opIndex, op.Removable);
            //possible improvement: try and remove everything inside the hint
            return false;
        }

        public void Reset()
        {
            _removableHints = new List<BlockStmt>();
            _removableCalcStmts = new List<CalcStmt>();
            _removableLines = new List<Expression>();
            _removableOps = new List<CalcStmt.CalcOp>();
        }
    }

    public interface IRemover
    {
        List<Wrap<T>> Remove<T>(Dictionary<MemberDecl, List<Wrap<T>>> memberWrapDictionary);
    }

    public class OneAtATimeRemover : IRemover
    {
        private readonly Program _program;

        public OneAtATimeRemover(Program program)
        {
            _program = program;
        }

        public List<Wrap<T>> Remove<T>(Dictionary<MemberDecl, List<Wrap<T>>> memberWrapDictionary)
        {
            var removableWraps = new List<Wrap<T>>();
            foreach (var wraps in memberWrapDictionary.Values) {
                removableWraps.AddRange(RemoveWraps(wraps.AsReadOnly()));
            }
            if (!IsProgramValid(_program))
                throw new Exception("Program invalid after removal!");
            return removableWraps;
        }

        private List<Wrap<T>> RemoveWraps<T>(ReadOnlyCollection<Wrap<T>> wraps)
        {
            return wraps.Where(TryRemove).ToList();
        }

        public bool TryRemove<T>(Wrap<T> wrap)
        {
            wrap.Remove();
            if (IsProgramValid(_program))
                return true;
            wrap.Reinsert();
            return false;
        }

        private static bool IsProgramValid(Program program)
        {
            var validator = new SimpleVerifier();
            return validator.IsProgramValid(program);
        }
    }

    /// <summary>
    /// Contains methods for dealing with the error information when 
    /// verification failed for reinserting the items on simultaneous 
    /// removal approaches
    /// </summary>
    internal abstract class VerificationErrorInformationRetriever
    {
        public abstract void ErrorInformation(ErrorInformation errorInfo);
        protected bool _cannotFindMemberException = false;

        public List<MemberDecl> AlreadyReAddedMembers = new List<MemberDecl>();

        protected MemberDecl FindMethod(int pos, IEnumerable<MemberDecl> members)
        {
            var memberDecls = members as IList<MemberDecl> ?? members.ToList();

            var foundMember = FindMethodInStandardBodies(pos, memberDecls);
            if (foundMember != null) return foundMember;
            CheckIfMemberWasAlreadyAdded(pos);
            foundMember = FindMemberInFunction(pos, memberDecls);
            if (foundMember != null) return foundMember;
            _cannotFindMemberException = true;
            throw new CannotFindMemberException();
        }

        private static MemberDecl FindMemberInFunction(int pos, IList<MemberDecl> memberDecls)
        {
            //Possible we're dealing with a function (or case where the token is before the start of the body)
            //Functions have no end tokens - (assuming the BodyEndTok thing isn't working)
            //we will look through all the functions and find the one with the highest pos that is less than the errors pos.
            MemberDecl foundMember = null;
            foreach (var memberDecl in memberDecls) {
                if (memberDecl.tok.pos > pos) continue; //function occurs after the token - no chance this is the one
                if (foundMember == null)
                    foundMember = memberDecl;
                else if (memberDecl.tok.pos > foundMember.tok.pos)
                    foundMember = memberDecl;
            }
            return foundMember;
        }

        private void CheckIfMemberWasAlreadyAdded(int pos)
        {
            //The method could not be found - maybe the removal caused two errors so we already have fixed it? lets be sure
            foreach (var member in AlreadyReAddedMembers) {
                if (member.BodyStartTok.pos <= pos && member.BodyEndTok.pos >= pos)
                    throw new AlreadyRemovedException();
                if (member.BodyStartTok.pos != 0 || member.BodyEndTok.pos != 0) continue; //Sometimes this bugs out...
                var method = member as Method;
                if (method == null) continue;
                if (method.Body.Tok.pos <= pos && method.Body.EndTok.pos >= pos)
                    throw new AlreadyRemovedException();
            }
        }

        private MemberDecl FindMethodInStandardBodies(int pos, IList<MemberDecl> memberDecls)
        {
            foreach (var member in memberDecls) {
                if (member.BodyStartTok.pos <= pos && member.BodyEndTok.pos >= pos) {
                    if (AlreadyReAddedMembers.Contains(member)) throw new AlreadyRemovedException();
                    return member;
                }
                if (member.BodyStartTok.pos != 0 || member.BodyEndTok.pos != 0)
                    continue; //Sometimes this bugs out... needs resolved first?
                var method = member as Method;
                if (method == null) continue;
                if (method.Body.Tok.pos <= pos && method.Body.EndTok.pos >= pos) {
                    if (AlreadyReAddedMembers.Contains(member)) throw new AlreadyRemovedException();
                    return member;
                }
            }
            return null;
        }
    }

    public class SimultaneousMethodRemover : IRemover
    {
        // Goes though each method, removes one thing then verifies and reinserts from the error messages
        private readonly Program _program;
        private readonly SimpleVerifier _simpleVerifier = new SimpleVerifier();

        internal class SimilRemoverStorage<T> : VerificationErrorInformationRetriever
        {
            public Dictionary<MemberDecl, Wrap<T>> LastRemovedItem = new Dictionary<MemberDecl, Wrap<T>>();

            public override void ErrorInformation(ErrorInformation errorInfo)
            {
                MemberDecl member;
                try {
                    member = FindMethod(errorInfo.Tok.pos, LastRemovedItem.Keys);
                }
                catch (AlreadyRemovedException) {
                    return;
                }

                if (member == null) return;
                LastRemovedItem[member].Reinsert();
                AlreadyReAddedMembers.Add(member);
                LastRemovedItem.Remove(member);
            }
        }

        public SimultaneousMethodRemover(Program program)
        {
            _program = program;
        }

        public List<Wrap<T>> Remove<T>(Dictionary<MemberDecl, List<Wrap<T>>> memberWrapDictionary)
        {
            var removableWraps = new List<Wrap<T>>();
            var index = 0;
            var similRemover = new SimilRemoverStorage<T>();
            var finished = false;
            while (!finished) {
                finished = RemoveAndTrackItemsForThisRun(memberWrapDictionary, index++, similRemover);
                RunVerification(similRemover);
                ReinsertInvalidItems(similRemover, removableWraps);
                similRemover.LastRemovedItem = new Dictionary<MemberDecl, Wrap<T>>();
                similRemover.AlreadyReAddedMembers = new List<MemberDecl>();
            }
            return removableWraps;
        }

        private static void ReinsertInvalidItems<T>(SimilRemoverStorage<T> similRemover, List<Wrap<T>> removableWraps)
        {
            removableWraps.AddRange(similRemover.LastRemovedItem.Values);
        }

        private void RunVerification<T>(SimilRemoverStorage<T> similRemover)
        {
            _simpleVerifier.IsProgramValid(_program, similRemover.ErrorInformation);
        }

        private bool RemoveAndTrackItemsForThisRun<T>(Dictionary<MemberDecl, List<Wrap<T>>> memberWrapDictionary,
            int index, SimilRemoverStorage<T> similRemover)
        {
            var finished = true;
            foreach (var method in memberWrapDictionary.Keys) {
                if (memberWrapDictionary[method].Count <= index) continue; //All items in this method have been done
                var wrap = memberWrapDictionary[method][index];
                wrap.Remove();
                similRemover.LastRemovedItem.Add(method, wrap); //Track the item
                finished = false;
            }
            return finished;
        }
    }

    internal class SimplificationWrapData
    {
        public List<Wrap<Statement>> RemovableAsserts = new List<Wrap<Statement>>();
        public List<Wrap<MaybeFreeExpression>> RemovableInvariants = new List<Wrap<MaybeFreeExpression>>();
        public List<Wrap<Expression>> RemovableDecreases = new List<Wrap<Expression>>();
        public List<Wrap<Statement>> RemovableLemmaCalls = new List<Wrap<Statement>>();
        public List<Wrap<Statement>> RemovableCalcs = new List<Wrap<Statement>>();
        public List<Tuple<Statement, Statement>> SimplifiedAsserts = new List<Tuple<Statement, Statement>>();

        public List<Tuple<MaybeFreeExpression, MaybeFreeExpression>> SimplifiedInvariants =
            new List<Tuple<MaybeFreeExpression, MaybeFreeExpression>>();

        public Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>> SimplifiedCalcs =
            new Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>>(new List<Expression>(),
                new List<BlockStmt>(), new List<CalcStmt.CalcOp>(), new List<CalcStmt>());

        public List<WildCardDecreases> RemovableWildCardDecreaseses = new List<WildCardDecreases>();

        public SimplificationData ToSimplificationData()
        {
            var simpData = new SimplificationData();
            foreach (var assert in RemovableAsserts)
                simpData.RemovableAsserts.Add((AssertStmt) assert.Removable);
            foreach (var invariant in RemovableInvariants)
                simpData.RemovableInvariants.Add(invariant.Removable);
            foreach (var decreases in RemovableDecreases)
                simpData.RemovableDecreases.Add(decreases.Removable);
            foreach (var lemmaCall in RemovableLemmaCalls)
                simpData.RemovableLemmaCalls.Add((UpdateStmt) lemmaCall.Removable);
            foreach (var calc in RemovableCalcs)
                simpData.RemovableCalcs.Add((CalcStmt) calc.Removable);
            foreach (var wildCard in RemovableWildCardDecreaseses)
                simpData.RemovableDecreases.Add(wildCard.Expression);
            foreach (var simplifiedAssert in SimplifiedAsserts)
                simpData.SimplifiedAsserts.Add(new Tuple<Statement, Statement>(simplifiedAssert.Item1,
                    simplifiedAssert.Item2));
            foreach (var simplifiedInvariant in SimplifiedInvariants)
                simpData.SimplifiedInvariants.Add(
                    new Tuple<MaybeFreeExpression, MaybeFreeExpression>(simplifiedInvariant.Item1,
                        simplifiedInvariant.Item2));
            simpData.SimplifiedCalcs = SimplifiedCalcs;
            return simpData;
        }
    }

    public class SimplificationData
    {
        public List<AssertStmt> RemovableAsserts = new List<AssertStmt>();
        public List<MaybeFreeExpression> RemovableInvariants = new List<MaybeFreeExpression>();
        public List<Expression> RemovableDecreases = new List<Expression>();
        public List<UpdateStmt> RemovableLemmaCalls = new List<UpdateStmt>();
        public List<CalcStmt> RemovableCalcs = new List<CalcStmt>();
        public List<Tuple<Statement, Statement>> SimplifiedAsserts = new List<Tuple<Statement, Statement>>();

        public List<Tuple<MaybeFreeExpression, MaybeFreeExpression>> SimplifiedInvariants =
            new List<Tuple<MaybeFreeExpression, MaybeFreeExpression>>();

        public Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>> SimplifiedCalcs;
    }

    internal class WildCardDecreasesRemover
    {
        private readonly Program _program;

        public WildCardDecreasesRemover(Program program)
        {
            _program = program;
        }

        public List<WildCardDecreases> FindRemovableWildCards(List<WildCardDecreases> wildCardDecreaseses)
        {
            var removableWildCards = new List<WildCardDecreases>();
            foreach (var wcDecreases in wildCardDecreaseses)
                removableWildCards.AddRange(FindRemovableWildCards(wcDecreases).Item1);
            return removableWildCards;
        }

        private Tuple<List<WildCardDecreases>, bool> FindRemovableWildCards(WildCardDecreases currentWildCardDecreases)
        {
            var removableWildCards = new List<WildCardDecreases>();
            var safeToRemove = true;
            RemoveWildCardSubDecreases(currentWildCardDecreases, removableWildCards, ref safeToRemove);

            if (safeToRemove)
                RemoveWildCardDecreases(currentWildCardDecreases, removableWildCards, ref safeToRemove);

            return new Tuple<List<WildCardDecreases>, bool>(removableWildCards, safeToRemove);
        }

        private void RemoveWildCardDecreases(WildCardDecreases currentWildCardDecreases,
            List<WildCardDecreases> removableWildCards, ref bool safeToRemove)
        {
            var index =
                currentWildCardDecreases.ParentSpecification.Expressions.IndexOf(currentWildCardDecreases.Expression);
            currentWildCardDecreases.ParentSpecification.Expressions.Remove(currentWildCardDecreases.Expression);
            var ver = new SimpleVerifier();
            if (ver.IsProgramValid(_program))
                removableWildCards.Add(currentWildCardDecreases);
            else {
                currentWildCardDecreases.ParentSpecification.Expressions.Insert(index,
                    currentWildCardDecreases.Expression);
                safeToRemove = false;
            }
        }

        private void RemoveWildCardSubDecreases(WildCardDecreases wcd, List<WildCardDecreases> removableWildCards,
            ref bool safeToRemove)
        {
            foreach (var subDec in wcd.SubDecreases) {
                var removableWCs = FindRemovableWildCards(subDec);
                removableWildCards.AddRange(removableWCs.Item1);
                if (safeToRemove)
                    safeToRemove = removableWCs.Item2;
            }
        }
    }

    public class MethodRemover
    {
        private readonly Program _program;

        public MethodRemover(Program program)
        {
            _program = program;
        }

        private bool IsProgramValid()
        {
            var verifier = new SimpleVerifier();
            return verifier.IsProgramValid(_program);
        }

        public SimplificationData FullSimplify(MemberDecl member)
        {
            var removableTypeFinder = new RemovableTypeFinder(_program);
            var removableTypesInMember = removableTypeFinder.FindRemovableTypesInSingleMember(member);

            var simpData = new SimplificationData();
            foreach (var assert in RemoveItems(removableTypesInMember.Asserts))
                simpData.RemovableAsserts.Add((AssertStmt) assert);
            simpData.RemovableInvariants.AddRange(RemoveItems(removableTypesInMember.Invariants));
            simpData.RemovableDecreases.AddRange(RemoveItems(removableTypesInMember.Decreases));
            foreach (var lemmaCall in RemoveItems(removableTypesInMember.LemmaCalls))
                simpData.RemovableLemmaCalls.Add((UpdateStmt) lemmaCall);
            simpData.SimplifiedAsserts.AddRange(SimplifyItems(removableTypesInMember.Asserts));
            simpData.SimplifiedInvariants.AddRange(SimplifyItems(removableTypesInMember.Invariants));
            var wcdRemover = new WildCardDecreasesRemover(_program);
            var wildCardDecreases = wcdRemover.FindRemovableWildCards(removableTypesInMember.WildCardDecreaseses);
            foreach (var wildCardDecrease in wildCardDecreases)
                simpData.RemovableDecreases.Add(wildCardDecrease.Expression);
            var calcLines = new List<Expression>();
            var calcBlocks = new List<BlockStmt>();
            var calcOps = new List<CalcStmt.CalcOp>();
            var allSimplifiedCalcs = new List<CalcStmt>();

            foreach (var wrap in removableTypesInMember.Calcs) {
                var simplifiedItem = SimplifyCalc((CalcStmt) wrap.Removable);
                if (simplifiedItem == null) continue;
                calcLines.AddRange(simplifiedItem.Item1);
                calcBlocks.AddRange(simplifiedItem.Item2);
                calcOps.AddRange(simplifiedItem.Item3);
                allSimplifiedCalcs.Add((CalcStmt) wrap.Removable);
            }
            simpData.SimplifiedCalcs =
                new Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>, List<CalcStmt>>(calcLines,
                    calcBlocks, calcOps, allSimplifiedCalcs);
            return simpData;
            //TODO remove things from the _allRemovableTypes. Would that even be worth it in this use case?
        }

        private List<T> RemoveItems<T>(List<Wrap<T>> wraps)
        {
            var removables = new List<T>();
            for (var i = wraps.Count - 1; i >= 0; i--) {
                var wrap = wraps[i];
                if (!RemoveItem(wrap)) continue;
                removables.Add(wrap.Removable);
                wraps.Remove(wrap);
            }
            return removables;
        }

        private List<Tuple<T, T>> SimplifyItems<T>(List<Wrap<T>> wraps)
        {
            var simplifiedItems = new List<Tuple<T, T>>();
            for (var i = wraps.Count - 1; i >= 0; i--) {
                var wrap = wraps[i];
                var simplifiedItem = SimplifyItem(wrap);
                if (simplifiedItem == null) continue;
                simplifiedItems.Add(simplifiedItem);
                wraps.Remove(wrap);
                wraps.Add(new Wrap<T>(simplifiedItem.Item2, wrap.ParentList));
            }
            return simplifiedItems;
        }

        private bool RemoveItem<T>(Wrap<T> wrap)
        {
            var index = wrap.ParentList.IndexOf(wrap.Removable);
            wrap.ParentList.Remove(wrap.Removable);
            var worked = IsProgramValid();
            if (!worked) {
                wrap.ParentList.Insert(index, wrap.Removable);
            }
            return worked;
        }

        private Tuple<T, T> SimplifyItem<T>(Wrap<T> wrap)
        {
            var simplifier = new Simplifier(_program);
            var simplifiedWraps = simplifier.TrySimplifyItem(wrap);
            if (simplifiedWraps == null) return null;
            return new Tuple<T, T>(simplifiedWraps.Item1.Removable, simplifiedWraps.Item2.Removable);
        }

        private Tuple<List<Expression>, List<BlockStmt>, List<CalcStmt.CalcOp>> SimplifyCalc(CalcStmt calc)
        {
            var calcRemover = new CalcRemover(_program);
            return calcRemover.SimplifyCalc(calc);
        }
    }

    internal class SimplificationItemInMethod
    {
        private enum Item
        {
            Assert,
            Invariant,
            Decreases,
            LemmaCall,
            Calc
        }

        private readonly Item _item;
        public MemberDecl Member;
        private readonly Wrap<Statement> _assert;
        private readonly Wrap<Statement> _lemmaCall;
        private readonly Wrap<Statement> _calcs;
        private readonly Wrap<MaybeFreeExpression> _invariant;
        private readonly Wrap<Expression> _decreases;

        public SimplificationItemInMethod(MemberDecl member, Wrap<Statement> statement)
        {
            if (statement.Removable is AssertStmt) {
                _assert = statement;
                _item = Item.Assert;
            }
            else if (statement.Removable is UpdateStmt) {
                _lemmaCall = statement;
                _item = Item.LemmaCall;
            }
            else if (statement.Removable is CalcStmt) {
                _calcs = statement;
                _item = Item.Calc;
            }
            Member = member;
        }

        public SimplificationItemInMethod(MemberDecl member, Wrap<MaybeFreeExpression> invariant)
        {
            _invariant = invariant;
            _item = Item.Invariant;
            Member = member;
        }

        public SimplificationItemInMethod(MemberDecl member, Wrap<Expression> deceases)
        {
            _decreases = deceases;
            _item = Item.Decreases;
            Member = member;
        }

        public object GetItem()
        {
            switch (_item) {
                case Item.Assert:
                    return _assert;
                case Item.Invariant:
                    return _invariant;
                case Item.Decreases:
                    return _decreases;
                case Item.Calc:
                    return _calcs;
                case Item.LemmaCall:
                    return _lemmaCall;
                default:
                    throw new Exception("item enum not found!");
            }
        }
    }

    internal class ConjunctionData
    {
        private int _index;
        public List<SimplificationItemInMethod> Brokenitems { get; private set; }
        public List<SimplificationItemInMethod> RequiredItems { get; private set; }
        public SimplificationItemInMethod OriginalItem { get; private set; }
        public SimplificationItemInMethod NewItem { get; private set; }
        private readonly MemberDecl _member;
        public bool Ignore { get; private set; }
        public bool Finished { get; private set; }

        //TODO: resinsert the new item into the program and remove the broken items
        public ConjunctionData(MemberDecl member, SimplificationItemInMethod item)
        {
            OriginalItem = item;
            _member = member;
            Finished = false;
            RequiredItems = new List<SimplificationItemInMethod>();

            var wrap = item.GetItem();
            if (wrap is Wrap<Statement>)
                Brokenitems = BreakDownWrap(wrap as Wrap<Statement>, member);
            else if (wrap is Wrap<MaybeFreeExpression>)
                Brokenitems = BreakDownWrap(wrap as Wrap<MaybeFreeExpression>, member);
            else throw new UnableToDetermineTypeException();
        }

        private List<SimplificationItemInMethod> BreakDownWrap<T>(Wrap<T> wrap, MemberDecl member)
        {
            var simpItems = new List<SimplificationItemInMethod>();

            var wraps = Simplifier.BreakDownExpr(wrap);
            Ignore = wraps.Count <= 1;
            if (Ignore) return null;
            wrap.Remove();
            foreach (var brokenWrap in wraps) {
                simpItems.Add(GetSimpItemFromWrap(member, brokenWrap));
            }
            return simpItems;
        }

        private static SimplificationItemInMethod GetSimpItemFromWrap<T>(MemberDecl member, Wrap<T> brokenWrap)
        {
            if (brokenWrap is Wrap<Statement>) {
                (brokenWrap as Wrap<Statement>).Reinsert();
                return new SimplificationItemInMethod(member, brokenWrap as Wrap<Statement>);
            }
            if (brokenWrap is Wrap<MaybeFreeExpression>) {
                (brokenWrap as Wrap<MaybeFreeExpression>).Reinsert();
                return new SimplificationItemInMethod(member, brokenWrap as Wrap<MaybeFreeExpression>);
            }
            throw new UnableToDetermineTypeException();
        }

        public void RemoveNext(Dictionary<MemberDecl, ConjunctionData> dictionary)
        {
            if (Ignore || _index >= Brokenitems.Count) {
                Finished = true;
                return;
            }
            //creates broken item to simpTypeThing and removes the item
            var item = Brokenitems[_index].GetItem();
            if (item is Wrap<Statement>)
                (item as Wrap<Statement>).Remove();
            else if (item is Wrap<MaybeFreeExpression>)
                (item as Wrap<MaybeFreeExpression>).Remove();
            else
                throw new UnableToDetermineTypeException();
            dictionary.Add(_member, this);
            _index++;
        }

        public void ReinsertLast()
        {
            var item = Brokenitems[_index - 1].GetItem();
            if (item is Wrap<Statement>) {
                var assertWrap = item as Wrap<Statement>;
                assertWrap.Reinsert();
                RequiredItems.Add(new SimplificationItemInMethod(_member, assertWrap));
            }
            else if (item is Wrap<MaybeFreeExpression>) {
                var invariantWrap = item as Wrap<MaybeFreeExpression>;
                invariantWrap.Reinsert();
                RequiredItems.Add(new SimplificationItemInMethod(_member, invariantWrap));
            }
            else
                throw new UnableToDetermineTypeException();
        }

        //required for slow removals if regular removals fail
        public void DecreaseIndex()
        {
            _index--;
        }

        public void CombineRequiredSubexpressions()
        {
            if (RequiredItems.Count == 0) return;
            var originalWrap = OriginalItem.GetItem();
            if (originalWrap is Wrap<Statement>) {
                var required =
                    RequiredItems.Select(requiredItem => requiredItem.GetItem() as Wrap<Statement>).ToList();
                required.Reverse();
                var newWrap = Simplifier.CombineToNewWrap(originalWrap as Wrap<Statement>, required);
                NewItem = new SimplificationItemInMethod(_member, newWrap);
            }
            else if (originalWrap is Wrap<MaybeFreeExpression>) {
                var requiredItems =
                    RequiredItems.Select(requiredItem => requiredItem.GetItem() as Wrap<MaybeFreeExpression>).ToList();
                requiredItems.Reverse();
                var newWrap = Simplifier.CombineToNewWrap(originalWrap as Wrap<MaybeFreeExpression>, requiredItems);
                NewItem = new SimplificationItemInMethod(_member, newWrap);
            }
            else throw new UnableToDetermineTypeException();
            //            originalWrap.Remove();
            //            newWrap.Reinsert();
            //            return new Tuple<T, T>(originalWrap.Removable, newWrap.Removable);
        }
    }

    internal class RemovableCalcParts //TODO: use this more
    {
        public CalcStmt OriginalCalcStmt;
        public List<Expression> Lines = new List<Expression>();
        public List<BlockStmt> Hints = new List<BlockStmt>();
        public List<CalcStmt.CalcOp> CalcOps = new List<CalcStmt.CalcOp>();

        public RemovableCalcParts(CalcStmt originalCalcStmt)
        {
            OriginalCalcStmt = originalCalcStmt;
        }
    }

    internal class CalcData
    {
        private readonly CalcStmt _calcStmt;

        public RemovableCalcParts RemovableCalcParts { get; private set; }
        public bool Finished { get; private set; }
        private int _lineIndex = 1, _hintIndex = 0; //lines start at 1 as first shouldn't be removed
        public readonly List<Expression> RemovedLines = new List<Expression>();
        public readonly List<BlockStmt> RemovedHints = new List<BlockStmt>();
        public readonly List<CalcStmt.CalcOp> RemovedCalcs = new List<CalcStmt.CalcOp>();

        private Expression _lastRemovedLine;
        private BlockStmt _lastRemovedHint;
        private CalcStmt.CalcOp _lastRemovedCalcOp;

        private List<Statement> _lastHintBody;

        private bool _linesComplete = false;
        private bool _dealtWithError = false;

        private int TokLine {
            get { return _calcStmt.Tok.line; }
        }

        public CalcData(CalcStmt calcStmt)
        {
            RemovableCalcParts = new RemovableCalcParts(calcStmt);
            _calcStmt = calcStmt;
            Finished = false;
        }

        public void RemoveNext()
        {
            _dealtWithError = false;
            GatherRemovableParts();
            if (!_linesComplete)
                RemoveNextLineAndHint();
            else
                EmptyNextHint();
        }

        private void RemoveNextLineAndHint()
        {
            if (_calcStmt.Lines.Count == _lineIndex + 1) {
                _linesComplete = true;
                _hintIndex = 0;
                EmptyNextHint();
                return;
            }

            if (_lastRemovedLine != null && _lastRemovedHint != null && _lastRemovedCalcOp != null) {
                _hintIndex = 0;
            }
            //bug: there are cases where an item could be removed but it would require combining two hints (e.g. ACL2-extractor.dfy line 63) - a way to undo this would also be needed
            _lastRemovedLine = _calcStmt.Lines[_lineIndex];
            _lastRemovedHint = _calcStmt.Hints[_hintIndex];
            _lastRemovedCalcOp = _calcStmt.StepOps[_hintIndex];

            _calcStmt.Lines.RemoveAt(_lineIndex);
            _calcStmt.Hints.RemoveAt(_hintIndex);
            _calcStmt.StepOps.RemoveAt(_hintIndex);
            // indexes not changed here - if they are successfully removed, the new ones will fall down to this position
        }

        private void GatherRemovableParts()
        {
            if (_lastRemovedLine != null)
                RemovableCalcParts.Lines.Add(_lastRemovedLine);
            if (_lastRemovedHint != null && _lastRemovedCalcOp != null) {
                if (_lastRemovedHint.Body.Count > 0) {
                    RemovableCalcParts.Hints.Add(_lastRemovedHint);
                    RemovableCalcParts.CalcOps.Add(_lastRemovedCalcOp);
                }
                else if (_lastHintBody != null) {
                    RefillLastHintBody();
                    RemovableCalcParts.Hints.Add(_lastRemovedHint);
                    RemovableCalcParts.CalcOps.Add(_lastRemovedCalcOp);
                }
            }

            _lastRemovedLine = null;
            _lastHintBody = null;
            _lastRemovedCalcOp = null;
        }

        private void EmptyNextHint()
        {
            if (_hintIndex >= _calcStmt.Hints.Count - 2) {
                Finished = true;
            }
            else if (_calcStmt.Hints[_hintIndex].Body.Count == 0) {
                _hintIndex++;
                EmptyNextHint();
            }
            else {
                EmptyHintBody(_calcStmt.Hints[_hintIndex]);
                _lastRemovedHint = _calcStmt.Hints[_hintIndex];
                _lastRemovedCalcOp = _calcStmt.StepOps[_hintIndex];
                _hintIndex++; //todo will this mess up?
            }
        }

        private void EmptyHintBody(BlockStmt hint)
        {
            _lastHintBody = new List<Statement>();
            var body = hint.Body;
            while (body.Count > 0) {
                _lastHintBody.Add(body[0]);
                body.RemoveAt(0);
            }
        }

        public void FailureOccured()
        {
            FailureOccured(true);
        }

        public void FailureOccuredDontUpdateIndexes()
        {
            FailureOccured(false);
        }

        private void FailureOccured(bool updateIndexes)
        {
            if (_dealtWithError) {
                return;
            }
            _dealtWithError = true;
            if (!_linesComplete) {
                ReinsertLastLineAndHint(updateIndexes);
            }
            else {
                RefillLastHintBody();
            }
        }

        private void ReinsertLastLineAndHint(bool updateIndexes)
        {
            _calcStmt.Lines.Insert(_lineIndex, _lastRemovedLine);
            _calcStmt.Hints.Insert(_hintIndex, _lastRemovedHint);
            _calcStmt.StepOps.Insert(_hintIndex, _lastRemovedCalcOp);

            _lastRemovedLine = null;
            _lastHintBody = null;
            _lastRemovedCalcOp = null;

            if(updateIndexes)
                UpdateIndexes();
        }

        private void UpdateIndexes()
        {
            _hintIndex++;
            if (_hintIndex <= _calcStmt.Hints.Count - 1) return;
            // All hints have been attempted to be removed with the line - this line cannot be removed
            _hintIndex = 0;
            _lineIndex++;
            if (_lineIndex >= _calcStmt.Lines.Count - 2)
                _linesComplete = true;
        }

        private void RefillLastHintBody()
        {
            foreach (var statement in _lastHintBody)
                _calcStmt.Hints[_hintIndex - 1].Body.Add(statement);

            _lastHintBody = null;
        }
    }

    internal class UnableToDetermineTypeException : Exception {}

    public class CannotFindMemberException : Exception {}

    internal class SimultaneousAllTypeRemover : VerificationErrorInformationRetriever
    {
        private readonly Program _program;
        private AllRemovableTypes _allRemovableTypes;
        private int _index;
        private bool _verificationException = false;

        private Dictionary<MemberDecl, SimplificationItemInMethod> _removedItemsOnRun =
            new Dictionary<MemberDecl, SimplificationItemInMethod>();

        private Dictionary<MemberDecl, WildCardDecreases> _wildCardDecreasesRemovedOnRun =
            new Dictionary<MemberDecl, WildCardDecreases>();

        private readonly Dictionary<MemberDecl, List<ConjunctionData>> _allConjunctions =
            new Dictionary<MemberDecl, List<ConjunctionData>>();

        private readonly Dictionary<MemberDecl, List<CalcData>> _allCalcs = new Dictionary<MemberDecl, List<CalcData>>();

        private readonly Dictionary<MemberDecl, int> _conjunctionIndex = new Dictionary<MemberDecl, int>();
        private readonly Dictionary<MemberDecl, int> _calcIndex = new Dictionary<MemberDecl, int>();

        private Dictionary<MemberDecl, ConjunctionData> _removedBrokenItems =
            new Dictionary<MemberDecl, ConjunctionData>();

        private Dictionary<MemberDecl, CalcData> _simplifiedCalcs = new Dictionary<MemberDecl, CalcData>();
        private StopChecker _stopChecker;

        public SimultaneousAllTypeRemover(Program program)
        {
            _program = program;
        }

        public override void ErrorInformation(ErrorInformation errorInfo)
        {
            var member = TryFindMember(errorInfo);
            if (member == null) return;
            AlreadyReAddedMembers.Add(member);

            if (_removedItemsOnRun.ContainsKey(member)) {
                ReinsertItemFromMember(member);
                _removedItemsOnRun.Remove(member);
            }
            else if (_wildCardDecreasesRemovedOnRun.ContainsKey(member)) {
                var wildCard = _wildCardDecreasesRemovedOnRun[member];
                wildCard.ExpressionWrap.Reinsert();
                _wildCardDecreasesRemovedOnRun.Remove(member);
                wildCard.CantBeRemoved = true;
            }
            else if (_removedBrokenItems.ContainsKey(member)) {
                //Reinsert the subexpression
                var conjunction = _removedBrokenItems[member];
                conjunction.ReinsertLast();
                if (!_allConjunctions.ContainsKey(member))
                    _allConjunctions.Add(member, new List<ConjunctionData>());
                if (!_allConjunctions[member].Contains(conjunction))
                    _allConjunctions[member].Add(conjunction);
                _removedBrokenItems.Remove(member);
                
            }
            else if (_simplifiedCalcs.ContainsKey(member))
                _simplifiedCalcs[member].FailureOccured();
            else
                throw new Exception("cant find member in removedOnRun");
        }

        private MemberDecl TryFindMember(ErrorInformation errorInfo)
        {
            try {
                var members = new List<MemberDecl>();
                members.AddRange(_removedItemsOnRun.Keys);
                members.AddRange(_wildCardDecreasesRemovedOnRun.Keys);
                members.AddRange(_removedBrokenItems.Keys);
                members.AddRange(_simplifiedCalcs.Keys);
                return FindMethod(errorInfo.Tok.pos, members);
            }
            catch (AlreadyRemovedException) {
                return null;
            }
        }

        private void ReinsertItemFromMember(MemberDecl member)
        {
            var item = _removedItemsOnRun[member].GetItem();
            ReinsertItem(item);
        }

        private static void ReinsertItem(object item)
        {
            if (item is Wrap<Statement>)
                ((Wrap<Statement>) item).Reinsert();
            else if (item is Wrap<MaybeFreeExpression>)
                ((Wrap<MaybeFreeExpression>) item).Reinsert();
            else if (item is Wrap<Expression>)
                ((Wrap<Expression>) item).Reinsert();
            else throw new UnableToDetermineTypeException();
        }

        public SimplificationData Remove(AllRemovableTypes allRemovableTypes, StopChecker stopChecker)
        {
            _stopChecker = stopChecker;
            return Remove(allRemovableTypes);
        }

        public SimplificationData Remove(AllRemovableTypes allRemovableTypes)
        {
            _allRemovableTypes = allRemovableTypes;
            var objectWraps = GetAllRemovableTypeItems(_allRemovableTypes);
            var simpWrapData = Remove(objectWraps);
            RemovedItemsFromAllRemovableTypes(simpWrapData);

            var simplificationData = simpWrapData.ToSimplificationData();
            return simplificationData;
        }

        private void RemovedItemsFromAllRemovableTypes(SimplificationWrapData simpWrapData)
        {
            foreach (var assert in simpWrapData.RemovableAsserts)
                _allRemovableTypes.RemoveAssert(assert);
            foreach (var invariant in simpWrapData.RemovableInvariants)
                _allRemovableTypes.RemoveInvariant(invariant);
            foreach (var decreases in simpWrapData.RemovableDecreases)
                _allRemovableTypes.RemoveDecreases(decreases);
            foreach (var lemmaCall in simpWrapData.RemovableLemmaCalls)
                _allRemovableTypes.RemoveLemmaCall(lemmaCall);
            foreach (var calc in simpWrapData.RemovableCalcs)
                _allRemovableTypes.RemoveCalc(calc);
            //TODO replace conjunctions
        }

        private static Dictionary<MemberDecl, List<SimplificationItemInMethod>> GetAllRemovableTypeItems(
            AllRemovableTypes allRemovableTypes)
        {
            return allRemovableTypes.RemovableTypesInMethods.Keys.ToDictionary(member => member,
                member => GetRemovableItemsInMethod(allRemovableTypes, member));
        }

        private static List<SimplificationItemInMethod> GetRemovableItemsInMethod(AllRemovableTypes allRemovableTypes,
            MemberDecl member)
        {
            var removableTypeInMethod = allRemovableTypes.RemovableTypesInMethods[member];
            var itemsInMethod =
                removableTypeInMethod.Asserts.Select(item => new SimplificationItemInMethod(member, item)).ToList();
            itemsInMethod.AddRange(
                removableTypeInMethod.Invariants.Select(item => new SimplificationItemInMethod(member, item)));
            itemsInMethod.AddRange(
                removableTypeInMethod.Decreases.Select(item => new SimplificationItemInMethod(member, item)));
            itemsInMethod.AddRange(
                removableTypeInMethod.LemmaCalls.Select(item => new SimplificationItemInMethod(member, item)));
            itemsInMethod.AddRange(
                removableTypeInMethod.Calcs.Select(item => new SimplificationItemInMethod(member, item)));
            return itemsInMethod;
        }

        private SimplificationWrapData Remove(Dictionary<MemberDecl, List<SimplificationItemInMethod>> simpItems)
        {
            var finished = false;
            var simpData = new SimplificationWrapData();
            while (!finished) {
                finished = RemoveAnItemFromEachMethod(simpItems);
                _index++;
                if (_stopChecker != null && _stopChecker.Stop) {
                    break;
                }
                VerifyProgram();
                if (_cannotFindMemberException || _verificationException) {
                    //Sometimes there can be a token somewhere other than in the method
                    //but it gets fixed by another token that is in the method.
                    //There are also cases where simultaneous removal fails so a SLow remove is tried instead
                    //if the program is not valid
                    var verifier = new SimpleVerifier();
                    if (!verifier.IsProgramValid(_program))
                        SlowRemoveInLeftOverMethods();
                }
                _cannotFindMemberException = false;
                GatherSimpData(simpData);
                Reset();
            }
            GatherConjunctionSimpData(simpData);
            GatherCalcSimpData(simpData);
            return simpData;
        }

        private void SlowRemoveInLeftOverMethods()
        {
            //This method is to improve stability if something goes wrong
            //It should allow the process to complete successfully but will be a lot slower.
            //TODO some kind of logging system could help id the errors

            SimpleVerifier verifier = new SimpleVerifier();
            
            foreach (var item in _removedItemsOnRun.Values)
            {
                var wrap = item.GetItem();
                if (wrap is Wrap<Statement>)
                    (wrap as Wrap<Statement>).Reinsert();
                else if (wrap is Wrap<MaybeFreeExpression>)
                    (wrap as Wrap<MaybeFreeExpression>).Reinsert();
                else if (wrap is Wrap<Expression>)
                    (wrap as Wrap<Expression>).Reinsert();
                else throw new UnableToDetermineTypeException();
            }

            foreach (var removedBrokenItem in _removedBrokenItems.Values) {
                removedBrokenItem.ReinsertLast();
                removedBrokenItem.DecreaseIndex();
            }

            foreach (var wildCardDecreasese in _wildCardDecreasesRemovedOnRun.Values) {
                wildCardDecreasese.ExpressionWrap.Reinsert();
            }

            foreach (var simplifiedCalc in _simplifiedCalcs.Values) {
                simplifiedCalc.FailureOccuredDontUpdateIndexes(); //TODO undo the indexing stuff.
            }

            if(!verifier.IsProgramValid(_program))
                throw new Exception("Program not valid after all reinsertions");

            var itemsToRemove = new List<MemberDecl>();
            foreach (var item in _removedItemsOnRun) {
                var wrap = item.Value.GetItem();
                if (wrap is Wrap<Statement>)
                    (wrap as Wrap<Statement>).Remove();
                else if (wrap is Wrap<MaybeFreeExpression>)
                    (wrap as Wrap<MaybeFreeExpression>).Remove();
                else if (wrap is Wrap<Expression>)
                    (wrap as Wrap<Expression>).Remove();
                else throw new UnableToDetermineTypeException();

                if (!verifier.IsProgramValid(_program)) {
                    if (wrap is Wrap<Statement>)
                        (wrap as Wrap<Statement>).Reinsert();
                    else if (wrap is Wrap<MaybeFreeExpression>)
                        (wrap as Wrap<MaybeFreeExpression>).Reinsert();
                    else
                        (wrap as Wrap<Expression>).Reinsert();
                    itemsToRemove.Add(item.Key);
                    
                }
            }

            //remove items (can't do them in previous foreach)
            foreach (var key in itemsToRemove) {
                _removedItemsOnRun.Remove(key);
            }

            itemsToRemove = new List<MemberDecl>();
            foreach (var member in _removedBrokenItems.Keys) {
                var conjunction = _removedBrokenItems[member];
                conjunction.RemoveNext(_removedBrokenItems);
                if (!verifier.IsProgramValid(_program))
                    conjunction.ReinsertLast();
                else {
                    if (!_allConjunctions.ContainsKey(member))
                        _allConjunctions.Add(member, new List<ConjunctionData>());
                    if (!_allConjunctions[member].Contains(conjunction))
                        _allConjunctions[member].Add(conjunction);
                    itemsToRemove.Add(member);
                }
            }

            foreach (var member in itemsToRemove) {
                _removedBrokenItems.Remove(member);
            }

            foreach (var wildCardDecreasese in _wildCardDecreasesRemovedOnRun.Values) {
                wildCardDecreasese.ExpressionWrap.Remove();
                if (verifier.IsProgramValid(_program)) continue;
                wildCardDecreasese.ExpressionWrap.Reinsert();
                wildCardDecreasese.CantBeRemoved = true;
            }
            foreach (var simplifiedCalc in _simplifiedCalcs.Values) {
                simplifiedCalc.RemoveNext();
                if (!verifier.IsProgramValid(_program))
                    simplifiedCalc.FailureOccured();
            }
        }

        private void GatherCalcSimpData(SimplificationWrapData simpData)
        {
            foreach (var allCalc in _allCalcs.Values) {
                foreach (var calc in allCalc) {
                    simpData.SimplifiedCalcs.Item1.AddRange(calc.RemovableCalcParts.Lines);
                    simpData.SimplifiedCalcs.Item2.AddRange(calc.RemovableCalcParts.Hints);
                    simpData.SimplifiedCalcs.Item3.AddRange(calc.RemovableCalcParts.CalcOps);
                    simpData.SimplifiedCalcs.Item4.Add(calc.RemovableCalcParts.OriginalCalcStmt);
                }
            }
        }

        private void GatherConjunctionSimpData(SimplificationWrapData simpData)
        {
            foreach (var conjunctionsInMethod in _allConjunctions.Values) {
                foreach (var conjunctionData in conjunctionsInMethod) {
                    GatherSimpDataFromConjunction(simpData, conjunctionData);
                }
            }
        }

        private static void GatherSimpDataFromConjunction(SimplificationWrapData simpData,
            ConjunctionData conjunctionData)
        {
            var originalItem = conjunctionData.OriginalItem;
            var requiredItems = conjunctionData.RequiredItems;
            conjunctionData.CombineRequiredSubexpressions();
            if (requiredItems.Count == 0 || requiredItems.Count == conjunctionData.Brokenitems.Count) return;
            var originalWrap = originalItem.GetItem();
            if (originalWrap is Wrap<Statement>)
                simpData.SimplifiedAsserts.Add(
                    new Tuple<Statement, Statement>(
                        (conjunctionData.OriginalItem.GetItem() as Wrap<Statement>).Removable,
                        (conjunctionData.NewItem.GetItem() as Wrap<Statement>).Removable));
            else if (originalWrap is Wrap<MaybeFreeExpression>)
                simpData.SimplifiedInvariants.Add(
                    new Tuple<MaybeFreeExpression, MaybeFreeExpression>(
                        (conjunctionData.OriginalItem.GetItem() as Wrap<MaybeFreeExpression>).Removable,
                        (conjunctionData.NewItem.GetItem() as Wrap<MaybeFreeExpression>).Removable));
        }


        private bool RemoveAnItemFromEachMethod(Dictionary<MemberDecl, List<SimplificationItemInMethod>> simpItems)
        {
            var finished = true;
            foreach (var member in simpItems.Keys) {
                var simpsInMethod = simpItems[member];
                if (!(_index < simpsInMethod.Count)) {
                    finished = FindRemoveableWildCards(member) && finished;
                    continue;
                }
                var item = simpsInMethod[_index].GetItem();
                if (item is Wrap<Statement>)
                    ((Wrap<Statement>) item).Remove();
                else if (item is Wrap<MaybeFreeExpression>)
                    ((Wrap<MaybeFreeExpression>) item).Remove();
                else if (item is Wrap<Expression>)
                    ((Wrap<Expression>) item).Remove();
                else
                    throw new UnableToDetermineTypeException();
                _removedItemsOnRun.Add(member, simpsInMethod[_index]);
                finished = false;
            }
            return finished;
        }

        private bool FindRemoveableWildCards(MemberDecl member)
        {
            var wildCards = _allRemovableTypes.RemovableTypesInMethods[member].WildCardDecreaseses;
            if (wildCards.Count < 1)
                return SimplifyItems(member);
            //Because all the wildcards are done we now move to conjunction simplification
            RemoveExtraWildCardDecreases(wildCards);
            if (wildCards[0].Simplified)
                _allRemovableTypes.RemoveWildCardDecreases(wildCards[0]);
            if (wildCards.Count == 0)
                return SimplifyItems(member);
            RemoveWildCard(wildCards[0], member);
            return false;
        }

        private void RemoveExtraWildCardDecreases(List<WildCardDecreases> wildCards)
        {
            if (wildCards.Count <= 1) return;
            for (var i = wildCards.Count - 1; i > 0; i--) {
                //remove all except first
                Contract.Requires(wildCards[i].SubDecreases.Count == 0);
                wildCards[i].ExpressionWrap.Remove();
                wildCards.Remove(wildCards[i]);
            }
        }

        private void RemoveWildCard(WildCardDecreases wildCard, MemberDecl member)
        {
            var canRemoveThis = true;
            foreach (var wildCardSub in wildCard.SubDecreases) {
                if (wildCardSub.Simplified) {
                    if (wildCardSub.CantBeRemoved)
                        canRemoveThis = false;
                    continue;
                }
                RemoveWildCard(wildCardSub, member);
                return;
            }
            if (canRemoveThis) {
                wildCard.ExpressionWrap.Remove();
                _wildCardDecreasesRemovedOnRun.Add(member, wildCard);
            }
            wildCard.Simplified = true;
        }


        private bool SimplifyItems(MemberDecl member)
        {
            if (!_conjunctionIndex.ContainsKey(member))
                _conjunctionIndex.Add(member, 0);
            var index = _conjunctionIndex[member];
            //Initialise the items if needed
            if (!_allConjunctions.ContainsKey(member)) {
                InitialiseConjunctions(member);
            }
            if (_allConjunctions.Count == 0 || index > _allConjunctions.Count)
                return SimplifyCalcs(member);
            foreach (var conj in _allConjunctions[member]) {
                if (conj.Finished) continue;
                conj.RemoveNext(_removedBrokenItems);
                if (!conj.Finished) return false;
                _conjunctionIndex[member]++;
            }
            return SimplifyCalcs(member);
        }

        private void InitialiseConjunctions(MemberDecl member)
        {
            var asserts = _allRemovableTypes.RemovableTypesInMethods[member].Asserts;
            var invariants = _allRemovableTypes.RemovableTypesInMethods[member].Invariants;
            _allConjunctions.Add(member, new List<ConjunctionData>());
            foreach (var assert in asserts)
                InitialiseItem(assert, member);
            foreach (var invariant in invariants)
                InitialiseItem(invariant, member);
        }

        private void InitialiseItem<T>(Wrap<T> wrap, MemberDecl member)
        {
            var conj = InitialiseBrokenItems(wrap, member);
            _allConjunctions[member].Add(conj);
        }

        private ConjunctionData InitialiseBrokenItems<T>(Wrap<T> wrap, MemberDecl member)
        {
            SimplificationItemInMethod simpItem;
            if (wrap is Wrap<Statement>)
                simpItem = new SimplificationItemInMethod(member, wrap as Wrap<Statement>);
            else if (wrap is Wrap<MaybeFreeExpression>)
                simpItem = new SimplificationItemInMethod(member, wrap as Wrap<MaybeFreeExpression>);
            else throw new UnableToDetermineTypeException();

            return new ConjunctionData(member, simpItem);
        }

        public Wrap<T> CombineAndInsertBrokenWraps<T>(Wrap<T> originalItem,
            List<SimplificationItemInMethod> brokenSimpItems)
        {
            var brokenItemWraps = new List<Wrap<T>>();
            foreach (var brokenSimpItem in brokenSimpItems) {
                var item = brokenSimpItem.GetItem();
                if (!(item is Wrap<T>)) throw new UnableToDetermineTypeException();
                var wrap = (Wrap<T>) item;
                wrap.Remove();
                brokenItemWraps.Add(wrap);
            }
            return Simplifier.CombineToNewWrap(originalItem, brokenItemWraps);
            // <--The item gets inserted to the parent inside here
        }

        private bool SimplifyCalcs(MemberDecl member)
        {
            var calcs = _allRemovableTypes.RemovableTypesInMethods[member].Calcs;
            if (calcs.Count == 0) return true;

            if (!_calcIndex.ContainsKey(member)) {
                _calcIndex.Add(member, 0);
            }
            var index = _calcIndex[member];
            if (index >= calcs.Count) return true;

            var calcData = GetCalcData(member);

            if (calcData.Finished) {
                _calcIndex[member]++;
                if (_calcIndex[member] >= calcs.Count)
                    return true;
            }

            calcData.RemoveNext();
            _simplifiedCalcs.Add(member, calcData);
            return false;
        }

        private CalcData GetCalcData(MemberDecl member)
        {
            var calcs = _allRemovableTypes.RemovableTypesInMethods[member].Calcs;
            var index = _calcIndex[member];

            if (!_allCalcs.ContainsKey(member))
                _allCalcs.Add(member, new List<CalcData>());

            if (_allCalcs[member].Count == index)
                _allCalcs[member].Add(new CalcData(calcs[index].Removable as CalcStmt));

            var calcData = _allCalcs[member][index];
            return calcData;
        }


        private void GatherSimpData(SimplificationWrapData simpData)
        {
            //everything still in itemsRemoved should be used for return
            foreach (var simplificationItemInMethod in _removedItemsOnRun.Values) {
                var item = simplificationItemInMethod.GetItem();
                if (item is Wrap<Statement>) {
                    var wrap = (Wrap<Statement>) item;
                    if (wrap.Removable is AssertStmt) {
                        simpData.RemovableAsserts.Add(wrap);
                        _allRemovableTypes.RemoveAssert(wrap);
                    }
                    else if (wrap.Removable is UpdateStmt) {
                        simpData.RemovableLemmaCalls.Add(wrap);
                        _allRemovableTypes.RemoveLemmaCall(wrap);
                    }
                    else if (wrap.Removable is CalcStmt) {
                        simpData.RemovableCalcs.Add(wrap);
                        _allRemovableTypes.RemoveCalc(wrap);
                    }
                    else
                        throw new UnableToDetermineTypeException();
                }
                else if (item is Wrap<MaybeFreeExpression>) {
                    var wrap = (Wrap<MaybeFreeExpression>) item;
                    simpData.RemovableInvariants.Add(wrap);
                    _allRemovableTypes.RemoveInvariant(wrap);
                }
                else if (item is Wrap<Expression>) {
                    var wrap = (Wrap<Expression>) item;
                    simpData.RemovableDecreases.Add(wrap);
                    _allRemovableTypes.RemoveDecreases(wrap);
                }
                else throw new UnableToDetermineTypeException();
            }
            foreach (var wildCard in _wildCardDecreasesRemovedOnRun.Values) {
                simpData.RemovableWildCardDecreaseses.Add(wildCard);
                _allRemovableTypes.RemoveWildCardDecreases(wildCard);
            }
            //conjunctions are not done here - all gathered at end 
            //TODO if the removed conjunction subExpressions are needed they can be retrieved here
        }

        private void Reset()
        {
            AlreadyReAddedMembers = new List<MemberDecl>();
            _removedItemsOnRun = new Dictionary<MemberDecl, SimplificationItemInMethod>();
            _wildCardDecreasesRemovedOnRun = new Dictionary<MemberDecl, WildCardDecreases>();
            _removedBrokenItems = new Dictionary<MemberDecl, ConjunctionData>();
            _simplifiedCalcs = new Dictionary<MemberDecl, CalcData>();
        }

        private void VerifyProgram()
        {
            try {
                SimpleVerifier.TryValidateProgram(_program, ErrorInformation);
            }
            catch (Exception) {
                _verificationException = true;
            }
        }
    }

    internal class AlreadyRemovedException : Exception {}

    internal class Simplifier
    {
        private readonly OneAtATimeRemover _remover;
        private readonly SimpleVerifier _verifier = new SimpleVerifier();
        private readonly Program _program;

        public Simplifier(Program program)
        {
            _program = program;
            _remover = new OneAtATimeRemover(program);
        }

        public List<Tuple<Wrap<T>, Wrap<T>>> GetSimplifiedItems<T>(IEnumerable<Wrap<T>> itemWraps)
        {
            var simplifiedItems = new List<Tuple<Wrap<T>, Wrap<T>>>();
            foreach (var wrap in itemWraps) {
                var simplifiedItem = TrySimplifyItem(wrap);
                if (simplifiedItem == null) continue;
                simplifiedItems.Add(simplifiedItem);
            }
            return simplifiedItems;
        }

        public Tuple<Wrap<T>, Wrap<T>> TrySimplifyItem<T>(Wrap<T> wrap)
        {
            var binExpr = GetExpr(wrap.Removable) as BinaryExpr;
            if (binExpr != null)
                if (binExpr.Op != BinaryExpr.Opcode.And)
                    return null; //Possible improvement: simplify when there is an implies by going deeper

            wrap.Remove();
            return !_verifier.IsProgramValid(_program) ? SimplifyItem(wrap) : null;
        }

        private Tuple<Wrap<T>, Wrap<T>> SimplifyItem<T>(Wrap<T> wrap)
        {
            var brokenItemWraps = BreakAndReinsertItem(wrap);
            var itemRemoved = false;
            //Test to see which can be removed
            for (var assertNum = brokenItemWraps.Count - 1; assertNum >= 0; assertNum--) {
                var brokenItem = brokenItemWraps[assertNum];
                var brokenWrap = new Wrap<T>(brokenItem.Removable, wrap.ParentList);
                if (!_remover.TryRemove(brokenWrap)) continue;
                brokenItemWraps.Remove(brokenItem);
                itemRemoved = true;
            }
            RemoveBrokenItemsFromParents(brokenItemWraps);
            if (!itemRemoved) {
                wrap.Reinsert();
                return null;
            }
            var newWrap = CombineToNewWrap(wrap, brokenItemWraps);
            return new Tuple<Wrap<T>, Wrap<T>>(wrap, newWrap);
        }

        public static Wrap<T> CombineToNewWrap<T>(Wrap<T> wrap, List<Wrap<T>> brokenItemWraps)
        {
            List<T> brokenItems = new List<T>();
            foreach (var brokenItemWrap in brokenItemWraps) {
                brokenItemWrap.Remove();
                brokenItems.Add(brokenItemWrap.Removable);
            }
            var newExpr = CombineItems(brokenItems);
            var newWrap = CreateNewItem(wrap, newExpr);
            return newWrap;
        }

        private void RemoveBrokenItemsFromParents<T>(List<Wrap<T>> brokenItemWraps)
        {
            foreach (var brokenItem in brokenItemWraps)
                brokenItem.Remove();
        }

        public static Wrap<T> CreateNewItem<T>(Wrap<T> wrap, Expression newExpr)
        {
            //Create a new item
            var newItem = GetNewNodeFromItem(wrap.Removable, newExpr);
            if (!wrap.Removed)
                wrap.Remove();
            //Insert the item
            if(wrap.Index < wrap.ParentList.Count)
                wrap.ParentList.Insert(wrap.Index, newItem);
            else
                wrap.ParentList.Add(newItem);
            //Wrap the new item
            var newWrap = new Wrap<T>(newItem, wrap.ParentList);
            return newWrap;
        }

        private List<Wrap<T>> BreakAndReinsertItem<T>(Wrap<T> wrap)
        {
            var brokenItems = BreakDownExpr(wrap);
            foreach (var brokenItem in brokenItems) {
                brokenItem.Reinsert();
            }
            return brokenItems;
        }

        public static List<Wrap<T>> BreakDownExpr<T>(Wrap<T> wrap)
        {
            var brokenItems = new List<Wrap<T>>();
            var binaryExpr = GetExpr(wrap.Removable) as BinaryExpr;
            if (binaryExpr == null || binaryExpr.Op != BinaryExpr.Opcode.And) {
                brokenItems.Add(wrap);
                return brokenItems;
            }

            //due to the fact that inserting at the required location will reverse the order of
            //the items, E1 is done first so that the order ends up the correct way around (this matters!)

            var newItem2 = GetNewNodeFromExpr(wrap, binaryExpr, binaryExpr.E1);
            var newItem1 = GetNewNodeFromExpr(wrap, binaryExpr, binaryExpr.E0);
            if (newItem2 != null) brokenItems.AddRange(BreakDownExpr(newItem2));
            if (newItem1 != null) brokenItems.AddRange(BreakDownExpr(newItem1));
            return brokenItems;
        }

        public static Expression GetExpr<T>(T removable)
        {
            var assert = removable as AssertStmt;
            if (assert != null) {
                return assert.Expr;
            }
            var invariant = removable as MaybeFreeExpression;
            if (invariant != null) {
                return invariant.E;
            }
            return null;
        }

        private static Wrap<T> GetNewNodeFromExpr<T>(Wrap<T> originalWrap, BinaryExpr binExpr, Expression subExpr)
        {
            var index = originalWrap.Removed
                ? originalWrap.Index
                : originalWrap.ParentList.IndexOf(originalWrap.Removable);
            var assert = originalWrap.Removable as AssertStmt;
            if (assert != null) {
                return new Wrap<T>((T) (object) new AssertStmt(binExpr.tok, assert.EndTok, subExpr, assert.Attributes),
                    originalWrap.ParentList, index);
            }
            var invariant = originalWrap.Removable as MaybeFreeExpression;
            if (invariant != null) {
                return new Wrap<T>((T) (object) new MaybeFreeExpression(subExpr), originalWrap.ParentList, index);
            }
            return null;
        }

        public static Expression CombineItems<T>(List<T> brokenItems)
        {
            if (brokenItems.Count < 1)
                return null; //null
            if (brokenItems.Count == 1)
                return GetExpr(brokenItems[0]);

            var item = brokenItems[0];
            brokenItems.Remove(item);
            var left = GetExpr(item);
            var right = CombineItems(brokenItems);


            var binExpr = new BinaryExpr(left.tok, BinaryExpr.Opcode.And, left, right);
            return binExpr;
        }

        private static T GetNewNodeFromItem<T>(T originalItem, Expression expr)
        {
            var assert = originalItem as AssertStmt;
            if (assert != null) {
                return (T) (object) new AssertStmt(assert.Tok, assert.EndTok, expr, assert.Attributes);
            }
            var invariant = originalItem as MaybeFreeExpression;
            if (invariant != null) {
                return (T) (object) new MaybeFreeExpression(expr);
            }
            throw new Exception("cant create a node from the item!");
        }
    }
}
