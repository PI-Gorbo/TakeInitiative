using JasperFx.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    //[Obsolete("Don't use a class to represent the beast but do shit when you read it from the DB", true)]
    public class Monster
    {

        public required string name { get; init; }
        public required string source { get; init; }

        public copy _copy { get; set; }

        public List<ac> AC { get; set; }
        public _type type { get; set; }

        public hp HP { get; set; }

    }

    public class hp
    {
        public int average { get; set; }
        public string formula { get; set; }
    }

    public class _type
    {
        public required string __type { get; init; }
    }

    public class action
    {
        public required string name { get; init; }
        public List<string> entries { get; set; }//TODO: Represent actions with a class instead?
    }
    public class ac
    {
        public required int Armour_Class { get; init; }
        public List<string> from { get; set; }
    }

    public class copy
    {
        public required string name { get; init; }
        public required string source { get; init; }

        public _mod mod { get; set; }

    }

    public class _mod
    {
        public List<modification> mods { get; set; }

    }

    public class modification
    {
        public required string mode { get; init; }
        public required string replace { get; init; }
        public required string with { get; init; }
        public required string flags { get; init; }

        //arabelle h as remove as action here???????
    }

    public class Root
    {
        public List<Monster> monsters { get; set; }
    }
    
}
