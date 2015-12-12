using System.Collections.Generic;
using Microsoft.Dafny;
using System.Diagnostics.Contracts;

namespace Tacny
{
    class LemmasAtomic : Atomic, IAtomicStmt
    {
        public LemmasAtomic(Atomic atomic) : base(atomic) { }



        public string Resolve(Statement st, ref List<Solution> solution_list)
        {
            return Lemmas(st, ref solution_list);
        }

        private string Lemmas(Statement st, ref List<Solution> solution_list)
        {
            IVariable lv = null;
            List<Expression> call_arguments; // we don't care about this
            List<Method> lemmas = new List<Method>();

            InitArgs(st, out lv, out call_arguments);
            Contract.Assert(lv != null, Util.Error.MkErr(st, 8));
            Contract.Assert(tcce.OfSize(call_arguments, 0), Util.Error.MkErr(st, 0, 0, call_arguments.Count));


            foreach (var member in program.members.Values)
            {
                Lemma lem = member as Lemma;
                FixpointLemma fl = member as FixpointLemma;
                if (lem != null)
                    lemmas.Add(lem);
                else if (fl != null)
                    lemmas.Add(fl);
                
            }
            IncTotalBranchCount();
            AddLocal(lv, lemmas);
            solution_list.Add(new Solution(this.Copy()));
            return null;
        }
    }
}
