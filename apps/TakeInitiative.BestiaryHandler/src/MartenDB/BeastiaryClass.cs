using JasperFx.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TakeInitiative.BestiaryHandler.src.Json;
using System.Collections;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    //[Obsolete("Don't use a class to represent the beast but do shit when you read it from the DB", true)]
    //[JsonProperty(PropertyName = "FooBar")]


    

    public class Monster
    {
        public string print_contents()
        {
            //string ret = "";
            //ret += string.Format("Name: {0}\n Source: {1}\n", name, source);
            //ret += "_copy: ";
            //ret += _copy.print_contents() ?? "null\n";
            //ret += "action: ";
            //if (action != null)
            //{
            //    ret += String.Join("", action);
            //}
            //else
            //{
            //    ret += "null\n";
            //}
            return JsonConvert.SerializeObject(this, Formatting.Indented);
            
            
        }
        public required string name { get; init; }
        public required string source { get; init; }

        public copy _copy { get; set; }
        public List<Action> action { get; set; }

        [JsonConverter(typeof(ACConverter))]
        public List<AC> ac { get; set; }
        [JsonConverter(typeof(ObjectOrStringConverter<Type>))]
        public object type { get; set; }

        public HP hp{ get; set; }
        

        public string cr { get; set; }
        public int str  { get; set; }
        public int dex  { get; set; }
        public int con  { get; set; }

        [JsonProperty(PropertyName = "int")]
        public int intelligence  { get; set; }
        public int wis  { get; set; }
        public int cha  { get; set; }

        public Save save { get; set; }

        public List<string> senses { get; set; }
        public int passive { get; set; }
        public List<string> immune { get; set; }
        public List<string> conditionImmune { get; set; }

        public List<Trait> trait { get; set; }

        public List<Reaction> reaction { get; set; }

        [JsonConverter(typeof(SkillConverter))]
        public Skill skill { get; set; }

        public List<string> languages { get; set; }

        //IDK WHY this is a list in the json file but it is
        //TODO: Add check for this
        public List<Spellcasting> spellcasting { get; set; }
    }

    public class Spellcasting
    {
        //see Baba Lysaga in cos 
        public string name { get; set; }
        public string type { get; set; }
        public List<string> headerEntries { get; set; }

        //TODO: this needs to be a custom converter
        [JsonConverter(typeof(BeastSpellSlotConverter))]
        public List<BeastSpellslot> spells { get; set; }

        //abiltiy is the spellcasting stat
        public string ability { get; set; }

    }

    //represents all the spells of a specific level for a beast/entity
    public class BeastSpellslot
    {
        public BeastSpellslot(string level, int slots, List<string> spells)
        {
            this.level = level;
            this.slots = slots;
            this.spells = spells;
        }
        //level is string instead of int because it is  a string key in the json file
        public string level { get; set; }
        //if slots is -1, it is cantrip/can be cast at will
        public int slots { get; set; }
        public List<string> spells { get; set; }

    }
    //only used in the custom BeastSpellslot converter
    //not to be used normally!
    public class BSS_Converter_Spell
    {
        public int slots;
        public List<string> spells;
    }

    public class Skill
    {
        //example skill object in the JSON file:
        /*
         * "skill": {
				"arcana": "+13",
				"religion": "+13"
			},
         
         */
        public Skill()
        {
            this.skills = new Dictionary<string, string>();
        }
        public Dictionary<string, string> skills;
    }
    
    public class Type
    {
        public string type { get; set; }
        //public List<string> tags { get; set; }
    }

    public class Reaction {
        public required string name { get; init; }
        public List<string> entries { get; set; }//TODO: Represent actions with a class instead?
    }
    public class Trait
    {
        public string name { get; set; }
        public List<string> entries { get; set; }
    }

    public class Save {
        public string str  { get; set; }
        public string dex  { get; set; }
        public string con  { get; set; }

        [JsonProperty(PropertyName = "int")]
        public string intelligence  { get; set; }
        public string wis  { get; set; }
        public string cha  { get; set; }


    }

    public class Fly {
        public Fly(int number)
        {
            this.number = number;
        }
        public int number { get; set;}
        public string condition {get; set;}
    }
    public class Speed
    {

        //TODO: MAKE A PROPER CONVERTER IN JSON HELPERS FOR CONSISTENCY?
        [JsonConstructor]
        public Speed(JToken Jfly) {
            if (Jfly.Type == JTokenType.Integer) {
                canHover = false;
                fly = new Fly((int)Jfly);
            }
            else {
                fly = Jfly.ToObject<Fly>();
            }
        }
        public int walk { get; set; }

        public Fly fly { get; set; }

        [DefaultValue(false)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool canHover {get; set;}
  
    }

    public class HP
    {
        public int average { get; set; }
        public string formula { get; set; }
    }
    public class Action
    {
        public required string name { get; init; }
        public List<string> entries { get; set; }//TODO: Represent actions with a class instead?
    }
    public class AC
    {
        public AC(int ac)
        {
            this.ac = ac;
        }
        //public required int ac { get; init; } = ac;
        public int ac { get; set; }
        public List<string> from { get; set; }
    }

    public class copy
    {
        public string print_contents()
        {

            string ret = String.Format("_copy Name: {0}\n _copy Source: {1}\n", name, source);
            ret += "_copy _mod: ";
            if (_mod != null)
            {
                ret += _mod.print_contents();
            }
            else
            {
                ret += "null\n";
            }
            return ret;
        }
        public required string name { get; init; }
        public required string source { get; init; }

        [JsonConverter(typeof(ModConverter))]
        public Mod _mod { get; set; }

    }

    public class Mod
    {

        public string print_contents()
        {
            string ret = "_mod *: ";
            if (ast != null)
            {
                ret += ast.print_contents();
            }
            else
            {
                ret += "null\n";
            }
            ret += "_mod PTM: ";
            if (possible_text_modifications == null)
            {
                return ret += "null\n";
            }
            foreach (var dict in possible_text_modifications)
            {
                var dictstr = string.Join("\n", dict);
                ret += dictstr;
                ret += "\n";
            }
            return ret;

        }
        public Mod()
        {
            possible_text_modifications = new List<Dictionary<string, Copy_Mod_Object>>();
        }
        [JsonProperty(PropertyName = "*")]
        public Asterisk ast { get; set; }
        //string would be trait or action or whatever
        public List<Dictionary<string, Copy_Mod_Object>> possible_text_modifications;
    }

    public class Asterisk
    {
        public string print_contents()
        {
            string ret = "asterisk: ";
            ret = "mode: " + mode + "\n replace: " + replace + "\n with: " + with + "\n flags: " + flags + "\n ";
            return ret;
        }
        public required string mode { get; init; }
        public required string replace { get; init; }
        public required string with { get; init; }
        public required string flags { get; init; }

        //arabelle has remove as action here???????
    }

    public class Copy_Mod_Object
    {
        public Copy_Mod_Object()
        {
            this.items = new List<Item>();
        }
        public override string ToString()
        {
            var str = "mode: " + mode + "\n replace: " + (replace ?? "null replace") + "\nindex: " + index + "\n items: " + (string.Join(", ", items) ?? "null items") + "\n ";
            return str;
        }

        public string mode { get; set; }
        public string replace { get; set; }
        public int index { get; set; }
        [JsonConverter(typeof(ItemConverter))]
        public List<Item> items { get; set; }
        //TODO: names can also be alist probably since these guys fuckign suck
        public string names { get; set; }
    }



    //TODO: Many classes here are copies of this class
    public class Item
    {
        //[JsonConstructor]
        //public Item(JToken token)
        //{ 
        //    if (token.Type == JTokenType.String)
        //    {
        //        this.name = token.ToString();
        //    }
        //}
        public Item(string name)
        {
            this.name = name;
            this.entries = new List<string>();
        }
        public override string ToString()
        {
            return "name: " + name + "\n entries: "  + (String.Join(", ", entries.ToArray()) ?? "null entries") + "\n ";
        }
        public string name { get; init; }
        public List<string> entries { get; set; }
    }


    public class Root
    {
        public List<Monster> monster { get; set; }
    }
    
}
