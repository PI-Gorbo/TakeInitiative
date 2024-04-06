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

    /// <summary>
    /// Represents all the spells of a specific level for a beast/entity
    /// </summary>
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
    /// <summary>
    ///Only used in the custom BeastSpellslot converter!
    ///<para>Not to be used normally!</para>
    /// </summary>

    public class BSS_Converter_Spell
    {
        public int slots;
        public List<string> spells;
    }

    /// <summary>
    ///Can either be a string of be its own JSON object
    ///<para>References:</para>
    ///<para>Autognome - beastiary-bam 1886</para>
    ///<para>Burney the Barber beastiary-bgdia - 1035</para>
    /// </summary>

public class Entry
    {
        public Entry(string str)
        {
            this.entry_string = str;
        }
        public Entry()
        {           
        }
        public string entry_string { get; set; }
        public string type { get; set; }
        public string style { get; set; }
        //[JsonConverter(typeof(Entry_Item_parentConverter))]
        public List<Entry_Item_parent> items { get; set; }

    }
    /// <summary>
    /// Either entry or entries will be null.
    /// <para>References for data:</para>
    /// <para>line 1040 of bestiary-bgdia.json - Burney the Barber</para>
    /// <para>line 1891 of beastiary-bam.json - Autognome</para>
    /// <para>line 3864 of beastiary-bmt.json - Living Portent</para>
    /// <para>child classes unused for now pending redesign </para>
    /// </summary>
    public class Entry_Item_parent
    {
        public Entry_Item_parent(string str)
        {
            this.entry = str;
        }
        public Entry_Item_parent(string type, List<string> list)
        {
            this.type = type;
            this.entries = list;
        }
        public string type { get; set; }
        public string name { get; set; }
        public List<string> entries { get; set; }
        public string entry { get; set; }

    }
    ///// <summary>
    ///// Reference: Autognome in Beastiary-Bam and Living Portent in beastiary-bmt
    ///// <para>Either entries or entry will be null/not set in the json</para>
    ///// </summary>
    //public class Entry_Item : Entry_Item_parent
    //{
    //    public string name { get; set; }
    //    public List<string> entries { get; set; }
    //    public string entry { get; set; }
    //}

    ///// <summary>
    ///// Reference: Burney the Barber in Beastiery-bgdia
    ///// </summary>
    //public class Entry_List : Entry_Item_parent
    //{
    //    public List<string> items { get; set; }
    //}


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
        //public string print_contents()
        //{

        //    string ret = String.Format("_copy Name: {0}\n _copy Source: {1}\n", name, source);
        //    ret += "_copy _mod: ";
        //    if (_mod != null)
        //    {
        //        ret += _mod.print_contents();
        //    }
        //    else
        //    {
        //        ret += "null\n";
        //    }
        //    return ret;
        //}
        public required string name { get; init; }
        public required string source { get; init; }


        public List<_template> _templates { get; set; }

        [JsonConverter(typeof(ModConverter))]
        public Mod _mod { get; set; }

    }

    /// <summary>
    ///This is the template JSON object as it appears in the beastiary json files!
    ///<para>NOT to be confused with the templates inside templates.json!</para>
    /// </summary>
    public class _template
    {
        public string name { get; set; }
        public string source { get; set; }
    }

    public class Mod
    {

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


        public string mode { get; set; }
        public string replace { get; set; }
        public int index { get; set; }
        [JsonConverter(typeof(ItemConverter))]
        public List<Item> items { get; set; }
        //TODO: names can also be alist probably since these guys fuckign suck
        public string names { get; set; }
    }


    /// <summary>
    /// ONLY USED IN COPY MOD OBJECTS. DO NOT CHANGE.
    /// <para>TODO: Many classes in BeastiaryClass.cs are copies of this class</para>
    /// <para>Possibly use this class to replace them?</para>>
    /// </summary>
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
            this.entries = new List<Entry_Item_parent>();
        }
        public string name { get; init; }
        [JsonConverter(typeof(Entry_Item_parentConverter))]
        //entries is a list of either a string or a JSON object
        public List<Entry_Item_parent> entries { get; set; }
    }


    public class Beast_Root
    {
        public List<Monster> monster { get; set; }
    }
    
}
