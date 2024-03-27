using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TakeInitiative.BestiaryHandler.src.MartenDB;

namespace TakeInitiative.BestiaryHandler.src.Json
{
    public class ModConverter : JsonConverter
    {
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
                else
                {
                    cmo = value.ToObject<Copy_Mod_Object>();
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
}
