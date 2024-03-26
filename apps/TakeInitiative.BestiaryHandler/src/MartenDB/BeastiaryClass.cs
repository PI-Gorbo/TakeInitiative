using JasperFx.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    //[Obsolete("Don't use a class to represent the beast but do shit when you read it from the DB", true)]
    //[JsonProperty(PropertyName = "FooBar")]
    public class Monster
    {

        public required string name { get; init; }
        public required string source { get; init; }

        public copy _copy { get; set; }

        public List<AC> ac { get; set; }
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
    public class AC
    {
        public required int ac { get; init; }
        public List<string> from { get; set; }
    }

    public class copy
    {
        public required string name { get; init; }
        public required string source { get; init; }

        public Mod _mod { get; set; }

    }

    public class Mod
    {
        [JsonProperty(PropertyName = "*")]
        public Asterisk ast { get; set; }
        public string action { get; set; }

    }


    public class Asterisk
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
