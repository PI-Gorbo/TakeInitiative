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
    public List<ArrItem> Items { get; set; } = new List<ArrItem>();
}



public class AppendArr : ICopyModifier
{
    public string mode { get; set; } = "appendArr";
    [JsonProperty("items")]
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
    public ArrModObject Replace { get; set; } 

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
    [JsonProperty("items")]
    public required List<ArrModObject> Items { get; set; } = new List<ArrModObject>();
}

public class  RemoveArr : ICopyModifier
{
    public string mode { get; set; } = "removeArr";
    //this can be something else but I am ignoring it and hoping the tech debt doesnt catch me later
    //since I dont see it used in a skim of the bestiary files
    [JsonProperty("names")]
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
public class ArrItem
{
    //name here is what category array of the base monster is being affected
    //eg if name is items then the items array of the monster if being modified
    [JsonProperty("name")]
    public required string Name { get; set; }
    [JsonProperty("entries")]
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