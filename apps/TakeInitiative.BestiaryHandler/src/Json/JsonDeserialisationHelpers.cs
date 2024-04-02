using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TakeInitiative.BestiaryHandler.src.MartenDB;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations;

namespace TakeInitiative.BestiaryHandler.src.Json
{
    /*
     * → JToken: It is the base class for all JSON tokens and represents a single JSON value like string, 
     * number, boolean, array, or object. It allows you to dynamically navigate through the JSON structure
     * and extract values without knowing the exact structure in advance. It is useful for working with JSON 
     * data flexibly and dynamically.

→     JObject: It is a subclass of JToken and specifically represents a JSON object. It behaves like a dictionary,
    allowing you to access and manipulate individual properties within the JSON object using key-value pairs. 
    It provides methods for adding, removing, or modifying properties. It is useful when you have a structured 
    JSON object with known properties and need direct access to specific properties.
     */


    public class BeastSpellSlotConverter: JsonConverter
    {
        public override bool CanWrite { get { return false; } }

        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            List<BeastSpellslot> list_bss = new List<BeastSpellslot>();
            JObject jobject = JObject.Load(reader);
            foreach (var KV in jobject)
            {
                string spell_level = KV.Key;
                JObject value = KV.Value.ToObject<JObject>();
                //check if slots is null - cantrips/ can cast at will spells, will not have a slots property in the json file
                int slots = -1;
                if (value["slots"] != null)
                {
                    slots = value["slots"].ToObject<int>();
                }
                
                //spells can't possibly be null
                List<string> spells = value["spells"].ToObject<List<string>>();
                BeastSpellslot bss = new BeastSpellslot(spell_level, slots, spells);
                list_bss.Add(bss);
            }
            return list_bss;


            

        }
    }
    public class SkillConverter: JsonConverter
    {
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
   
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            Skill skill = new Skill();
            JObject jobject = JObject.Load(reader);
            foreach (var KV in jobject)
            {
                string key = KV.Key;
                string value = KV.Value.ToString();
                skill.skills[key] = value;
            }
            return skill;
        
        }
    }

    public class ItemConverter : JsonConverter
    {
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
        //pretty much handle string cases
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            List<Item> items = new List<Item>();
            JToken token = JToken.Load(reader);
            //handle "items": "cold"
            if (token.Type == JTokenType.String)
            {
                items.Add(new Item(token.ToString()));
            }
            //handle "items" : {JSONOBJECT}
            else if (token.Type == JTokenType.Object)
            {
                items.Add(token.ToObject<Item>());
            }
            //if items is an array
            else
            {
                foreach(JToken array_item in token)
                {
                    items.Add(array_item.ToObject<Item>());
                }
            }
            return items;
        }
    }
    
    public class ModConverter : JsonConverter
    {
        public override bool CanWrite { get { return false; } }
        public Copy_Mod_Object handle_one_or_many_items(JToken value)
        {
            //DEBUG LOG
            using (StreamWriter sw = File.AppendText(@"E:\\SCHOOL STUFF\\CODING\\C#\\log.txt"))
            {
                sw.WriteLine("value is {0}", value.ToString());

            }
            Copy_Mod_Object cmo = new Copy_Mod_Object();

            //set mode
            cmo.mode = value["mode"].ToString();
            //object might not have replace field
            if (value["replace"] != null)
            {
                cmo.replace = value["replace"].ToString();
            }
            //object might not have index field
            if (value["index"] != null)
            {
                cmo.index = value["index"].ToObject<int>();
            }
            //object might not have names field
            if (value["names"] != null)
            {
                cmo.names = value["names"].ToString();
            }
            //if object doesn't have items field, return immediately
            if (value["items"] == null) {
                return cmo;
            }

            cmo = value.ToObject<Copy_Mod_Object>();

            /*
            //check if it has only one item or a list of items
            if (value["items"].Type == JTokenType.Object)
            {
                //replace with generic to object? remove whole if compeltely?         
                cmo.items.Add(value["items"].ToObject<Item>());
            }
            else
            {
                cmo = value.ToObject<Copy_Mod_Object>();
            }
            */
            return cmo;
        }
        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jobject = JObject.Load(reader);
            Mod mod = new Mod();
            //check if asterisk exists
            if (jobject["*"] != null) {
                mod.ast = jobject["*"].ToObject<Asterisk>();
            }
            //loop through everything else in the JObject
            foreach (var x in jobject)
            {
                //ignore the asterisk key
                if (x.Key == "*")
                {
                    continue;
                }
                string key = x.Key;
                JToken value = x.Value;
                //check if json is something like "action": "remove"
                Copy_Mod_Object cmo = new Copy_Mod_Object();
                if (value.Type == JTokenType.String)
                {
                    //value.ToString should be "remove" or something like that
                    cmo.mode = value.ToString();
                }
                // json here should be something like 
                /*
                 * "action": {
                        "mode": "replaceArr",
                        "replace": "Longbow",
                        "items": {
                            "name": "Light Crossbows",
                            "entries": [
                                "{@atk rw} {@hit 4} to hit, ranged 80/320 ft., one target. {@h}6 ({@damage 1d8 + 2}) piercing damage."
                            ]
                        }
                    }
                 
                 */
                /* OR
                 * "trait": {
						"mode": "appendArr",
						"items": [
							{
								"name": "Fey Ancestry",
								"entries": [
									"Kasimir has advantage on saving throws against being {@condition charmed}, and magic can't put the him to sleep."
								]
							},
							{
								"name": "Special Equipment",
								"entries": [
									"Kasimir wears a {@item ring of warmth} and carries a spellbook containing all the spells he has prepared plus the following spells: {@spell arcane lock}, {@spell comprehend languages}, {@spell hold person}, {@spell identify}, {@spell locate object}, {@spell nondetection}, {@spell polymorph}, {@spell protection from evil and good}, and {@spell wall of stone}."
								]
							}
						]
					}
                 */
                //EDGE CASE WHERE THE VALUE IS A LIST 
                /* EG 
                 "action": [
						{
							"mode": "replaceArr",
							"replace": "Multiattack",
							"items": {
								"name": "Multiattack",
								"entries": [
									"The knight makes two longsword attacks or two fist attacks."
								]
							}
						},
						{
							"mode": "insertArr",
							"index": 1,
							"items": {
								"name": "Longsword",
								"entries": [
									"{@atk mw} {@hit 7} to hit, reach 5 ft., one target. {@h}15 ({@damage 2d10 + 4}) slashing damage. If the target is a creature against which the knight has sworn vengeance, the target takes an extra 14 ({@damage 4d6}) slashing damage."
								]
							}
						}
					]
                 */
                //WHY TF CAN THIS BE AN ARRAY OR A JSON OBJECT?? WHY???
                else if (value.Type == JTokenType.Array)
                {
                    //TODO THIS IS PRETTY CRINGE AND SHIT
                    //append the dict for everything but the last array value, which will be done at the outside of this if block
                    int length = (value as JArray).Count;
                    for (int i = 0; i < length - 1; i++)
                    {
                        cmo = handle_one_or_many_items(value[i]);
                        var mul_dict = new Dictionary<string, Copy_Mod_Object>()
                        {
                            { key, cmo }
                        };
                        mod.possible_text_modifications.Add(mul_dict);

                    }
                    //setup the last cmo value like every other if statement here

                    cmo = handle_one_or_many_items(value[length - 1]);

                    
                }
                else
                {
                    //debug log
                    using (StreamWriter sw = File.AppendText(@"E:\\SCHOOL STUFF\\CODING\\C#\\log.txt"))
                    {
                        sw.WriteLine("value is {0}", value.ToString());

                    }
                    cmo = handle_one_or_many_items(value);
                    
                    
                }
                //create new dictionary with key and CMO and add to the list stored in the Mod object
                var dict = new Dictionary<string, Copy_Mod_Object>()
                {
                    { key, cmo }
                };
                mod.possible_text_modifications.Add(dict);


            }
            return mod;

        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

    }

    
    public class ACConverter : JsonConverter
    {
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(System.Type objectType)
        {
            // CanConvert is not called when the [JsonConverter] attribute is used
            return false;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {

            JToken token = JToken.Load(reader);
            using (StreamWriter sw = File.CreateText(@"E:\\SCHOOL STUFF\\CODING\\C#\\log.txt"))
            {
                sw.WriteLine("token value is {0}", token.ToString());
                
            }
            //IDK why this is a list in the json file
            var ac_list = new List<AC>();
            //assumes the token is a jarray
            foreach (JToken item in token)
            {
                if (item.Type == JTokenType.Integer)
                {
                    ac_list.Add(new AC(item.ToObject<int>()));
                }
                else
                {
                    ac_list.Add(item.ToObject<AC>());
                }

            }

            return ac_list;

            


            
            
        }
    }


    public class ObjectOrStringConverter<T> : JsonConverter
    {
        public override bool CanWrite { get { return false; } }
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


}
