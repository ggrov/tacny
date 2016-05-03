using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;
using System;
using Tacny;
using System.Diagnostics;

namespace LazyTacny
{
    class LemmasAtomic : Atomic, IAtomicLazyStmt
    {
        public LemmasAtomic(Atomic atomic) : base(atomic) { }

        public IEnumerable<Solution> Resolve(Statement st, Solution solution)
        {
            
            yield return Lemmas(st, solution);
            
            yield break;
        }

        private Solution Lemmas(Statement st, Solution solution)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<MemberDecl> lemmas = new List<MemberDecl>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));


            foreach (var member in StaticContext.program.members.Values)
            {
                Lemma lem = member as Lemma;
                FixpointLemma fl = member as FixpointLemma;
                if (lem != null)
                    lemmas.Add(lem);
                else if (fl != null)
                    lemmas.Add(fl);
                
            }
            AddLocal(lv, lemmas);
            return new Solution(this.Copy());
        }
    }
}
