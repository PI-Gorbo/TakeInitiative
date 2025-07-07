using System;

using TakeInitiative.Bestiary.Domain.Helpers;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TakeInitiative.Bestiary.Domain.JSON;


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



//For some reason dex isnt being deserialised properly so this is kinda for testing
class DexConverter : BoilerplateConverter {
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken JToken = JToken.Load(reader);
        if (JToken.Type == JTokenType.Null)
        {
            return null;
        }
        else if (JToken.Type == JTokenType.Integer)
        {
            return JToken.ToObject<int>();
        }
        else
        {
            return Int32.MaxValue;
        }
    }

}



// need hp,  ac name source initiative
//Returns a monster object
class MonsterConverter : BoilerplateConverter
    {
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        
        //JToken jtoken = JToken.Load(reader);
        JObject jobject = JObject.Load(reader);

        //if no hp and no copy then return null
        //Example is Elzerina cassalanter from WDH v2.9.1
        //literally bugged object
        if (jobject["_copy"] == null && jobject["hp"] == null) {
            return null;
        }

        StopGapMonsterClass monster = jobject.ToObject<StopGapMonsterClass>();

        //check if monster object has _copy property
        //if so, set IsCopy to true which will prevent some post serialisation methods from running and throwing errors
        //why does the first one not work bruh
        #region this shit DOESNT work the jobject.toobject fires the ondeserialization before this code fires!
        //if (jobject.TryGetValue("_copy", out _))
        //{
            
        //    monster.IsCopy = true;
        //    throw new Exception("omg it works");
        //}
        //if (jobject["_copy"] != null)
        //{
        //    monster.IsCopy = true;
        //    throw new Exception("please work");
        //}
        #endregion

        //Debug.WriteLine("1 is " + jobject.TryGetValue("_copy", out _) + "2 is " + jobject["_copy"] +  " for " + monster.Name);
        return monster;
    }


}

//convert a single json object into acitem
//returns type I_acItem
class ACConverter : BoilerplateConverter {
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
       
        JToken jtoken = JToken.Load(reader);
        if (jtoken.Type == JTokenType.Integer) {
            return new AcObject() {Ac = jtoken.ToObject<int>() };
        } else if (jtoken.Type == JTokenType.Object) {
            return jtoken.ToObject<AcObject>();
        } else {
            throw new JsonSerializationException("Unknown AC type: " + jtoken.Type);
        }

    }
}


//public class InitiativeConverter : JsonConverter {
//    #region boilerplate jsonconverter stuff
//    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//    {
//        serializer.Serialize(writer, value);
//    }
//    public override bool CanWrite
//    {
//        get { return false; }
//    }

//    public override bool CanConvert(Type objectType)
//    {
//        return false;
//    }
//    #endregion

//    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
//        JToken jtoken = JToken.Load(reader);



//    }

//}

class CrConverter : BoilerplateConverter{       
    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken jtoken = JToken.Load(reader);
        if (jtoken.Type == JTokenType.String)
        {
            string numstring = jtoken.ToString();
            
            //edge case: For some reason V2.9.1 "Mechanical Bird" just has the cr rating "Unknown" in the json???
            if (numstring == "Unknown")
            {
                return new CrObject { Cr = 0 };
            }

            double rating = 0;
            if (numstring.Contains('/')) {
                //assume it's a fraction in string form
                rating = helpers.FractionToDouble(numstring);
            }
            else
            {
                rating = double.Parse(numstring);
            }

            return new CrObject { Cr = rating };

        }
        else
        {
            return jtoken.ToObject<CrObject>();
        }


    }
}

/// <summary>
/// TEMP FUNCTION to handle post deserialisation methods that break if the monster is a copy!
/// In the future we will properly handle the data in the _copy field instead of checking and then ignoring it!
/// </summary>
class _copyConverter : BoilerplateConverter
{
    

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken jtoken = JToken.Load(reader);
        if (jtoken.Type != JTokenType.Null)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}



//INTERMEDIATE CLASS for json converters - since the first 3 overrides are exactly the same for all of them
 class BoilerplateConverter : JsonConverter
{
    #region boilerplate jsonconverter stuff
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
    public override bool CanWrite
    {
        get { return false; }
    }

    public override bool CanConvert(Type objectType)
    {
        return false;
    }
    #endregion

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        throw new Exception("DO NOT USE THIS CLASS");
    }
}
