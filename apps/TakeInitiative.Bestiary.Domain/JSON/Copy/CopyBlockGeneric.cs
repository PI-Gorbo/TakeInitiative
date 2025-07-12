using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace TakeInitiative.Bestiary.Domain.JSON.Copy;

/// <summary>
/// This is the top level "_copy" object found in the Monster Object.
/// The only fields that are required as per the schema are the name and source fields
/// </summary>
class CopyBlockGeneric
{
    //TODO: add _mod object
    [JsonProperty("_mod")]
    //MOD OBJECT FIELD HERE

    //TODO: Add the "_preserve" field and figure out how it fits in
    //preserve seems to preserve some fields from the original monster
    //but like.... isn't that done by default when the monster is copied?
    [JsonProperty("_templates")]
    public List<NameSourcePair> templates { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("source")]
    public required string Source { get; set; }

}

/// <summary>
/// This class represents a json object that contains a name field and a source field.
/// </summary>
public class NameSourcePair
{
    [JsonProperty("name")]
    public required string Name { get; set; }
    [JsonProperty("source")]
    public required string Source { get; set; }
}


public class ModObject 
{
    //GlobalMods apply to the entire monster and are not specific to a single trait of the monster (I think)
    [JsonProperty("*")]
    public List<ICopyModifier> GlobalMods { get; set; } = new List<ICopyModifier>();


}