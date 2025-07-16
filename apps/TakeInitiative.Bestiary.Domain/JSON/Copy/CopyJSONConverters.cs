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
        else if (jtoken.Type == JTokenType.String)
        {
            var str = jtoken.ToString();
            if (str == "remove")
            {
                return null;
            }
        }
        else {
            Debug.WriteLine($"Expected JObject or JArray, but got {jtoken.Type}");
            throw new JsonSerializationException($"jtoken value is {jtoken.Value<String>()}");
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
            case "removeArr":
                return jobject.ToObject<RemoveArr>(serializer);
            case "replaceArr":
                return jobject.ToObject<ReplaceArr>(serializer);
            case "insertArr":
                return jobject.ToObject<InsertArr>(serializer);
            default:
                throw new JsonSerializationException($"Unknown mode: {jobject["mode"]}");
        }
    }

}


public class ArrItemConverter : BoilerplateConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken jtoken = JToken.Load(reader);
        List<ArrItem> arrItems = new List<ArrItem>();
        if (jtoken.Type == JTokenType.Object)
        {
            arrItems.Add(jtoken.ToObject<ArrItem>());
        }
        else if (jtoken.Type == JTokenType.Array)
        {
            arrItems = jtoken
                .Children<JObject>()
                .Select(j =>
                {
                    Debug.WriteLine($" array item is {j.ToString()}");
                    return j.ToObject<ArrItem>();
                }).ToList();
        }
        else
        {
            throw new JsonSerializationException($"Expected JObject or JArray, but got {jtoken.Type}");
        }
        return arrItems;
    }
}

public class  ArrItemEntryConverter : BoilerplateConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        
        JToken jtoken = JToken.Load(reader);
        if (jtoken.Type == JTokenType.String)
        {
            return jtoken.ToObject<string>();
        }
        //fuck you 5etools
        //there is a SINGLE entry that
        //for some godforsaken reason, instead of being a string inside an array
        //decides to have its OWN json object inside said array
        //so this code handles that
        else if (jtoken.Type == JTokenType.Object)
        {
            var jobject = jtoken.ToObject<JObject>();
            return string.Join("\n", jobject["items"]);
        }
        else
        {
            throw new JsonSerializationException($"Expected an array item to either be an object or string but got {jtoken.Value<string>()}");
        }
    }
}

public class ReplaceArrConverter :  BoilerplateConverter
{
    //Important:
    //CanConvert does not get called when you mark something with [JsonConverter].
    //When you use the attribute, Json.Net assumes you have provided the correct converter,
    //so it doesn't bother with the CanConvert check. 
    //However, since we are calling this converter manually in ArrModObjectOrArrayConverter,
    //We need to actually tell it that the base interface class is in face convertable
    public override bool CanConvert(Type objectType)
    {
        return (objectType == typeof(ArrModObject));
    }
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        
        JToken jtoken = JToken.Load(reader);

        if (jtoken.Type == JTokenType.Object)
        {
            var jobject = jtoken.ToObject<JObject>();
            if (jobject.ContainsKey("index"))
            {
                return jobject.ToObject<IndexArrReplace>();
            }
            else if (jobject.ContainsKey("regex"))
            {
                return jobject.ToObject<RegexArrReplace>();
            }
            else if (jobject.ContainsKey("name"))
            {
                return jobject.ToObject<ArrItem>();
            }
            else
            {
                throw new JsonSerializationException($"Expected JObject with 'index' or 'regex' or 'name' key, but got {jtoken.ToString()}");
            }
        }
        else if (jtoken.Type == JTokenType.String)
        {
            return new StringArrReplace() { String = jtoken.ToString() };
        }
        else
        {
            throw new JsonSerializationException($"Expected JObject or String, but got {jtoken.Type}");
        }
    }
}


public class ArrModObjectOrArrayConverter : BoilerplateConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {

        //This is how we instruct the serializer to use our other custom jsonconverter
        //To give us our desired ArrModObject
        var replaceArrConverter = new ReplaceArrConverter();
        serializer.Converters.Add(replaceArrConverter);
        List<ArrModObject> list = new List<ArrModObject>();
        JToken jtoken = JToken.Load(reader);

        if (jtoken.Type == JTokenType.Object)
        {
            var p = JsonConvert.DeserializeObject<ArrModObject>(jtoken.ToString(), replaceArrConverter);
            //list.Add(jtoken.ToObject<ArrModObject>(serializer));
            list.Add(p);
        }
        else if (jtoken.Type == JTokenType.Array)
        {
            list = jtoken
                .Children<JToken>()
                .Select( j=> {
                    //return j.ToObject<ArrModObject>(serializer);
                    return JsonConvert.DeserializeObject<ArrModObject>(j.ToString(), replaceArrConverter);
                })
                .ToList();
        }
        else 
        {
            throw new JsonSerializationException($"Expected JObject or JArray, but got {jtoken.Type}");
        }
        return list;
        
    }
}   


/// <summary>
/// Converts a json token that is either a string, or an array of strings, into a List<string>
/// </summary>
public class StringOrArrToStringArrConverter : BoilerplateConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        List<String> lst = new List<string>();
        JToken jtoken = JToken.Load(reader);
        if (jtoken.Type == JTokenType.String)
        {
            lst.Add(jtoken.ToString());
        }
        else if (jtoken.Type == JTokenType.Array)
        {
            lst =  jtoken.ToObject<List<string>>();
        }
        else
        {
            throw new JsonSerializationException($"Expected String or Array of Strings, but got {jtoken.Type}");
        }
        return lst;
    }
}