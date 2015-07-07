using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dafny;
using Dafny = Microsoft.Dafny;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using System.Diagnostics.Contracts;

namespace Tacny
{
    public class Main
    {


        /// <summary>
        /// Applies tactics in the program
        /// </summary>
        /// <param name="dafnyProgram"></param>
        /// <param name="fileNames"></param>
        /// <param name="programId"></param>
        /// <param name="stats"></param>
        /// <returns></returns        
        public static string ResolveProgram(ref Program tacnyProgram)
        {
            Interpreter r = new Interpreter(tacnyProgram);

            // If the program does not have tactics, run the standard translation/validation and exit
            if (r.HasTactics() && TacnyOptions.O.ResolveTactics)
            {
                String err = r.ResolveProgram();
                if (err != null)
                    return err;

                tacnyProgram.MaybePrintProgram(DafnyOptions.O.DafnyPrintResolvedFile);
            }
            else
            {
                tacnyProgram.ResolveProgram();
                tacnyProgram.VerifyProgram();
            }




            // Everything is ok
            return null;
        }
    }

}
