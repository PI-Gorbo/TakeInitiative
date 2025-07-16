using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace TakeInitiative.Bestiary.Domain.JSON.Copy;


//holy cancer
//wtf this spec 
//Spec/Reference for _copy objects and how they work is below:
//https://github.com/TheGiddyLimit/homebrew/blob/master/_doc/Spec_Copy.md
//IMPORTANT: This code is expected to break sooner or later (but likely sooner)
//The spec is really vague (eg this json field can either be an object, array, or string)
//But does not tell us what kind of object or objects it will contain!
//And some objects are used like ONCE in the entire bestiary so 
//there aren't even enough examples to figure out what the hell is going on



public class ReplaceTxt : ICopyModifier
{
    //mode MUST be replaceTxt otherwise it is not this object type
    public string mode { get; set; } = "replaceTxt";
    public required string replace { get; set; }
    public required string with { get; set; }
    public string flags;

    //TODO: Stupid fucking edge case in werejaguar TftYP regarding usage of the field "props" (used ONE time in the entire bestiary)
}

public class ReplaceName : ICopyModifier
{
    //mode MUST be replaceName otherwise it is not this object type
    public string mode { get; set; } = "replaceName";
    public required string replace { get; set; }
    public required string with { get; set; }
    public string flags { get; set; }
}

public class AppendStr : ICopyModifier
{
    public string mode { get; set; } = "appendStr";
    public string str { get; set; }
    public string joiner { get; set; }
}

public class PrependArr : ICopyModifier
{
    public string mode { get; set; } = "prependArr";
    [JsonProperty("items")]
    [JsonConverter(typeof(ArrItemConverter))]
    public List<ArrItem> Items { get; set; } = new List<ArrItem>();
}



public class AppendArr : ICopyModifier
{
    public string mode { get; set; } = "appendArr";
    [JsonProperty("items")]
    [JsonConverter(typeof(ArrItemConverter))]
    public List<ArrItem> Items { get; set; } = new List<ArrItem>();
}

public class AppendIfNotExistsArr : ICopyModifier
{
    public string mode { get; set; } = "appendIfNotExistsArr";
    [JsonProperty("items")]
    public List<ArrItem> Items { get; set; } = new List<ArrItem>();
}

public class ReplaceArr : ICopyModifier
{
    public string mode { get; set; } = "replaceArr";
    //replace seems to be string most of the time but the spec says it can be other things
    [JsonProperty("replace")]
    [JsonConverter(typeof(ReplaceArrConverter))]
    public ArrModObject Replace { get; set; }
    [JsonProperty("items")]
    [JsonConverter(typeof(ArrItemConverter))]
    public List<ArrItem> Items { get; set; } = new List<ArrItem>();

}

public class ReplaceOrAppendArr : ICopyModifier
{
    public string mode { get; set; } = "replaceOrAppendArr";
    //replace seems to be string most of the time but the spec says it can be other things
    [JsonProperty("replace")]
    public ArrModObject Replace { get; set; }
    [JsonProperty("items")]
    public required List<ArrModObject> Items { get; set; } = new List<ArrModObject>();
}

public class InsertArr : ICopyModifier 
{
    public string mode { get; set; } = "insertArr";
    [JsonProperty("index")]
    public required int Index { get; set; }
    [JsonConverter(typeof(ArrModObjectOrArrayConverter))]
    public required List<ArrModObject> Items { get; set; } = new List<ArrModObject>();
}

public class  RemoveArr : ICopyModifier
{
    public string mode { get; set; } = "removeArr";
    //Jsonconverter must take into account names can either be an array of strings or a string by itself
    [JsonProperty("names")]
    [JsonConverter(typeof(StringOrArrToStringArrConverter))]
    public required List<string> Names { get; set; } = new List<string>();
}



#region Interfaces
public interface ICopyModifier
{
    //This interface is used to mark classes that are copy modifiers
    //and to ensure that they have a mode field
    public string mode { get; set; }
}
public interface ArrModObject 
{

}
#endregion

#region classes that are children of the "mode" classes
//ArrItem is used in objects that want to modify a monster's arrays
public class ArrItem : ArrModObject
{
    //name here is what category array of the base monster is being affected
    //eg if name is items then the items array of the monster if being modified
    [JsonProperty("name")]
    public required string Name { get; set; }
    [JsonProperty(ItemConverterType = typeof(ArrItemEntryConverter), PropertyName = "entries")]
    
    public required List<string> Entries { get; set; }
}

public class IndexArrReplace : ArrModObject
{
    [JsonProperty("index")]
    public required int Index { get; set; }
}

public class RegexArrReplace : ArrModObject
{
    [JsonProperty("regex")]
    public required string Regex { get; set; }
    [JsonProperty("flags")]
    public required string Flags { get; set; }
}

public class StringArrReplace : ArrModObject
{
    public string String { get; set; } = string.Empty;
}

#endregion