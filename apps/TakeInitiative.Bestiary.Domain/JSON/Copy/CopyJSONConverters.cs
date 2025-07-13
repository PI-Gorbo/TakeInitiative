using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TakeInitiative.Bestiary.Domain.JSON;
namespace TakeInitiative.Bestiary.Domain.JSON.Copy;


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
public class ICopyModifierConverter : BoilerplateConverter
{
    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        List<ICopyModifier> copymods = new List<ICopyModifier>();
        JToken jtoken = JToken.Load(reader);


        if (jtoken.Type == JTokenType.Object)
        {
            copymods.Add(ConvertJobject(jtoken.ToObject<JObject>(), serializer));
        }
        else if (jtoken.Type == JTokenType.Array)
        {
            copymods = jtoken
                .Children<JObject>()
                .Select(j =>
                {
                    return ConvertJobject(j, serializer);
                }).ToList();
        }
        else { 
            throw new JsonSerializationException($"Expected JObject or JArray, but got {jtoken.Type}");
        }
        return copymods;
    }

    private ICopyModifier ConvertJobject(JObject jobject, JsonSerializer serializer)
    {
        string mode = jobject["mode"].ToString();
        switch (mode)
        {
            case "replaceTxt":
                return jobject.ToObject<ReplaceTxt>(serializer);
            case "replaceName":
                return jobject.ToObject<ReplaceName>(serializer);
            case "appendStr":
                return jobject.ToObject<AppendStr>(serializer);
            case "prependArr":
                return jobject.ToObject<PrependArr>(serializer);
            case "appendArr":
                return jobject.ToObject<AppendArr>(serializer);
            default:
                throw new JsonSerializationException($"Unknown mode: {jobject["mode"]}");
        }
    }

}