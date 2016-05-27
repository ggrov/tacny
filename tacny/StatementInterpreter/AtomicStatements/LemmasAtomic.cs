using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Microsoft.Dafny;
using Util;

namespace Tacny
{
    class LemmasAtomic : Atomic, IAtomicStmt
    {
        public LemmasAtomic(Atomic atomic) : base(atomic) { }



        public void Resolve(Statement st, ref List<Solution> solution_list)
        {
            Lemmas(st, ref solution_list);
        }

        private void Lemmas(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<MemberDecl> lemmas = new List<MemberDecl>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Error.MkErr(st, 0, 0, call_arguments.Count));


            foreach (var member in globalContext.program.Members.Values)
            {
                Lemma lem = member as Lemma;
                FixpointLemma fl = member as FixpointLemma;
                if (lem != null)
                    lemmas.Add(lem);
                else if (fl != null)
                    lemmas.Add(fl);
                
            }
            AddLocal(lv, lemmas);
            solution_list.Add(new Solution(Copy()));
        }
    }
}
