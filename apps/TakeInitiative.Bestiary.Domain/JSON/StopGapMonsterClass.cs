using System.Diagnostics.Eventing.Reader;
using System.Runtime.Serialization;
using TakeInitiative.Bestiary.Domain.Helpers;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TakeInitiative.Bestiary.Domain.JSON;


public class MonsterRoot {
    //Itemconvertertype is used here so that each item in the list is converted using the MonsterConverter
    [JsonProperty(ItemConverterType = typeof(MonsterConverter), PropertyName = "monster")]
    public List<StopGapMonsterClass> Monsters { get; set; } = [];
}
public class StopGapMonsterClass
{
    [JsonProperty("source")]
    public required string Source { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    public string _5etools_link { get; set; } = "";

    [JsonProperty("hp")]
    public required Hp Hp { get; set; }
    [JsonProperty(ItemConverterType = typeof(ACConverter), PropertyName = "ac")]
    public required List<AcObject> Ac { get; set; }
    
    [JsonProperty("initiative")]
    public InitiativeObject Initiative { get; set; }

    //TODO: Apparently ability scores can be "null" But I dont know where or when or why or how
    //wait we dont even need a class we can just make it a nullable int in the class lol
    //public required abilityScore dex;
    [JsonConverter(typeof(DexConverter))]
    [JsonProperty("dex")]
    public required int? Dex;

    [JsonConverter(typeof(_copyConverter))]
    [JsonProperty("_copy")]
    public bool? IsCopy { get; set; }

    [JsonProperty("cr")]
    [JsonConverter(typeof(CrConverter))]
    public required CrObject Cr { get; set; }

    [OnDeserialized]
    internal void onDeserialized(StreamingContext context)
    {
        //only calc initiative if this is not a copy of another monster in json (will throw error because missing data from deserialisation)
        //this is stupid btw I was checking it was null and it wasnt or some shit it behaved in a way i did not understand
        //so i kinda just /shrug
        //this seems to work so i leave
        if (!IsCopy.HasValue)
        {
            this.CalculateInitiative();
        }
        this.calc_link();
    }


    //callback function: Calculate dex AFTER deserialising the rest of the class!
    internal void CalculateInitiative() {
        //debugging: This shouldnt run if dex is null in the first place!
        if (!Dex.HasValue)
        {
            throw new Exception(this.Name + "has no dex score but trying to calculate initiative!");
        }
        //calculate dex modifier
        int dexmod = ((Dex.Value - 10) / 2);
        //if no initiative modifiers then calculate from dexmod only
        if (Initiative == null)
        {
            Initiative = new InitiativeObject();
            Initiative.Initiative = dexmod;
        }
        //if the json actually has an initiative dont mess with it
        else if (Initiative.Initiative.HasValue)
        {
            return;
        }
        else
        {
            //check if there is a proficiency bonus and add dex
            if (Initiative.Proficiency.HasValue)
            {
                //get prof bonus from cr
                int prof_bonus = helpers.GetProficiencyBonus(Cr.Cr);
                //add proficiency bonus x amount of times
                //where x is stored in the json
                //then add dexmod
                Initiative.Initiative = (prof_bonus * Initiative.Proficiency.Value) + dexmod;
            }

        }
    }

    //callback function: calculate 5etools link after deserialisation
    internal void calc_link()
    {
        this._5etools_link = helpers.build_link(Name, Source);
    }
}


public class Hp {
    [JsonProperty("average")]
    public int Average { get; set; }
    [JsonProperty("formula")]
    public string Formula { get; set; }
}

public class AcObject
{

    public required int Ac { get; set; }
    public List<string> From { get; set; } = [];

    public string Condition { get; set; } = string.Empty;
    public bool Braces { get; set; }
    


}

//public class ac_int : I_acItem {
//    public int ac; 

//    public int get_ac() {
//        return ac;
//    }
//    public ac_int(int ac) {
//        this.ac = ac;
//    }
//}

//will have to calculate from dex score if not explicitly mentioned
public class InitiativeObject {
    public int? Initiative;

    public int? GetInitiative() {
        return Initiative;
    }

    public void SetInitiative(int initiative) {
        this.Initiative = initiative;
    }

    public int? Proficiency { get; set; }

    //-1 for dis, 0 for normal, 1 for advantage
    public string AdvantageMode { get; set; }
    public int GetAdvantage() {
        if (string.IsNullOrEmpty(AdvantageMode))
        {
            return 0;
        }

        else if (AdvantageMode == "adv")
        {
            return 1;
        }
        else if (AdvantageMode == "dis")
        {
            return -1;
        }
        else
        {
            throw new Exception("Unknown advantage value, posible deserialisation error"); 
            
        }
    }


}


//the cr string class will be abstracted into this
public class CrObject
{

    public required double Cr { get; set; }
    public string Lair { get; set; }
    public string Coven { get; set; }
    public int Xp { get; set; }
    public int XpLair { get; set; }

}

public class special_object {
    public string special { get; set; }
}

//public interface I_acItem {
//    int get_ac();
//}


//public interface I_initiative
//{
//    int get_initiative();
//    int get_advantage();

//}


