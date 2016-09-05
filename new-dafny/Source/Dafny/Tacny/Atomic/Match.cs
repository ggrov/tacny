using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Microsoft.Dafny;

namespace Tacny.Atomic{
  class Match : Atomic{
    public override string Signature => "tmatch";
    public override int ArgsCount => -1;

    private List<string> names;

    public static List <string> ParseDefaultCasesNames(Statement stmt){
      return null;
    }

    public Match(Statement stmt) {
      names = ParseDefaultCasesNames(stmt);
    }

    public override IEnumerable<ProofState> Generate(Statement statement, ProofState state){

        //check which cases need to apply tacny


        //for each brnache, generate code but assume false state for other branches

        throw new NotImplementedException();
        

    }


  }
}