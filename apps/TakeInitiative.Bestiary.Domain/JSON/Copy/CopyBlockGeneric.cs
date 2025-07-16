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
public class CopyBlockGeneric
{
    
    [JsonProperty("_mod")]
    public ModObject Mod { get; set; } = new ModObject();

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
    //GlobalMods or "*" 
    // apply to all text properties
    // text properties are: "action", "reaction", "trait", "legendary", "variant", and "spellcasting"
    //The converter must take into account that the array in this field can sometimes be a standalone object

    //As a TEMP HACK, if the value isnt a json object but a astring eg: "remove"
    //then the converter will return null

    [JsonProperty(PropertyName ="*")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> GlobalMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "action")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> ActionMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "reaction")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> ReactionMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "trait")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> TraitMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "legendary")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> LegendaryMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "variant")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> VariantMods { get; set; } = new List<ICopyModifier>();

    [JsonProperty(PropertyName = "spellcasting")]
    [JsonConverter(typeof(ICopyModifierConverter))]
    public List<ICopyModifier> SpellcastingMods { get; set; } = new List<ICopyModifier>();



}