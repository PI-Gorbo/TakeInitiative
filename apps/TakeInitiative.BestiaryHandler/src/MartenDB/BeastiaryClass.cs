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

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    //[Obsolete("Don't use a class to represent the beast but do shit when you read it from the DB", true)]
    //[JsonProperty(PropertyName = "FooBar")]


    

    public class Monster
    {

        public required string name { get; init; }
        public required string source { get; init; }

        public copy _copy { get; set; }
        public List<Action> action { get; set; }

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

    }
    
    public class Type
    {
        public string type { get; set; }
        public List<string> tags { get; set; }
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

        //THIS MIGHT NOT WORK
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
        public required int ac { get; init; }
        public List<string> from { get; set; }
    }

    public class copy
    {
        public required string name { get; init; }
        public required string source { get; init; }

        [JsonConverter(typeof(ModConverter))]
        public Mod _mod { get; set; }

    }

    public class Mod
    {

        public string print_contents()
        {
            string ret = "";
            if (ast != null)
            {
                ret += "*: ";
                ret += ast.print_contents();
            }
            foreach (var dict in possible_text_modifications)
            {
                var dictstr = string.Join(Environment.NewLine, dict);
                ret += "\nPTM: \n";
                ret += dictstr;
            }
            return ret;

        }
        public Mod()
        {
            possible_text_modifications = new List<Dictionary<string, Copy_Mod_Object>>();
        }
        [JsonProperty(PropertyName = "*")]
        public Asterisk ast { get; set; }
        public List<Dictionary<string, Copy_Mod_Object>> possible_text_modifications;
    }

    public class Asterisk
    {
        public string print_contents()
        {
            string ret = "";
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
        public override string ToString()
        {
            var str = "mode: " + mode + "\n replace: " + replace + "\n items: " + items + "\n ";
            return str;
        }

        public string mode { get; set; }
        public string replace { get; set; }
        public Item items { get; set; }
    }



    //TODO: Many classes here are copies of this class
    public class Item
    {
        public override string ToString()
        {
            return "name: " + name + "\n entries: "  + String.Join(", ", entries.ToArray()) + "\n ";
        }
        public required string name { get; init; }
        public List<string> entries { get; set; }
    }


    public class Root
    {
        public List<Monster> monster { get; set; }
    }
    
}
