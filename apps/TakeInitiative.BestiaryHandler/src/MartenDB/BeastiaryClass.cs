using JasperFx.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    //[Obsolete("Don't use a class to represent the beast but do shit when you read it from the DB", true)]
    //[JsonProperty(PropertyName = "FooBar")]
    public class ObjectOrStringConverter<T> : JsonConverter
    {
        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Object)
            {
                return token.ToObject<T>(serializer);
            }
            return token.ToString();
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

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

        public Mod _mod { get; set; }

    }

    public class Mod
    {
        [JsonProperty(PropertyName = "*")]
        public Asterisk ast { get; set; }
        
        //TODO: HEre action can be iether a string or the class
        //additionally, it can be many names not just action - eg trait
        public ModAction action { get; set; }

        public List<Item> items { get; set; }
        public Mod_Replacement trait { get; set; }

    }

    public class ModAction
    {

        public string mode { get; set; }
        public string replace { get; set; }
        public List<Item> items { get; set; }
    }

    //TODO: Many classes here are copies of this class
    public class Item
    {
        public required string name { get; init; }
        public List<string> entries { get; set; }
    }

    public class Mod_Replacement
    {
        public string mode { get; set; }
        public string replace { get; set; }
        public Item items { get; set; }
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
        public List<Monster> monster { get; set; }
    }
    
}
