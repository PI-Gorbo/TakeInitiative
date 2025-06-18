//using JasperFx.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.CodeAnalysis;

namespace BestiaryAPI.JSON
{
    public class Bestiary
    {
        //public List<Alignment> align { get; set; }

    }
    public class SpecialProperty
    {
        public string? special { get; set; }
    }
    public class alignment
    {
        enum Alignments
        {
            L,
            N,
            NX,
            NY,
            C,
            G,
            E,
            U,
            A
        }
        //static AlignDict = new Dictionary<Alignments, String>() {

        //    }

    }

    

    public class abilityScore {
        //is either an int/null or special
        public Nullable<int>  score { get; set; }
    }

    public class _legendaryActions {
        public int num { get; set; }
    }

    public class creatureData {
        
    }
    

}

