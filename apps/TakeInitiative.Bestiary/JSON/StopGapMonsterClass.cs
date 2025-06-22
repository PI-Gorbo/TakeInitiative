using System.Diagnostics.Eventing.Reader;
using System.Runtime.Serialization;

using BestiaryAPI.helpers;

using Microsoft.AspNetCore.Routing.Constraints;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BestiaryAPI.JSON

{
    public class Monster_root {
        //Itemconvertertype is used here so that each item in the list is converted using the MonsterConverter
        [JsonProperty(ItemConverterType = typeof(MonsterConverter), PropertyName = "monster")]
        public List<StopGapMonsterClass> Monsters { get; set; }
    }
    public class StopGapMonsterClass
    {
        [JsonProperty("source")]
        public required string Source { get; set; }

        [JsonProperty("name")]
        public required string Name { get; set; }

        public string _5etools_link { get; set; } = "";

        [JsonProperty("hp")]
        public required hp Hp { get; set; }
        [JsonProperty(ItemConverterType = typeof(ACConverter), PropertyName = "ac")]
        public required List<I_acItem> Ac { get; set; }
        
        [JsonProperty("initiative")]
        public initiative_object Initiative { get; set; }

        //TODO: Apparently ability scores can be "null" But I dont know where or when or why or how
        //wait we dont even need a class we can just make it a nullable int in the class lol
        //public required abilityScore dex;
        [JsonProperty("dex")]
        public required int? dex;

        [JsonConverter(typeof(_copyConverter))]
        [JsonProperty("_copy")]
        public bool? IsCopy { get; set; }

        [JsonProperty("cr")]
        [JsonConverter(typeof(CrConverter))]
        public required cr_object cr { get; set; }

        [OnDeserialized]
        internal void onDeserialized(StreamingContext context)
        {
            //only calc initiative if this is not a copy of another monster in json (will throw error because missing data from deserialisation)
            if (!IsCopy.HasValue)
            {
                this.calculate_initiative();
            }
            this.calc_link();
        }


        //callback function: Calculate dex AFTER deserialising the rest of the class!
        internal void calculate_initiative() {
            //debugging: This shouldnt run if dex is null in the first place!
            if (!dex.HasValue)
            {
                throw new Exception(this.Name + "has no dex score but trying to calculate initiative!");
            }
            //calculate dex modifier
            int dexmod = ((dex.Value - 10) / 2);
            //if no initiative modifiers then calculate from dexmod only
            if (Initiative == null)
            {
                Initiative = new initiative_object();
                Initiative.initiative = dexmod;
            }
            //if the json actually has an initiative dont mess with it
            else if (Initiative.initiative.HasValue)
            {
                return;
            }
            else
            {
                //check if there is a proficiency bonus and add dex
                if (Initiative.proficiency.HasValue)
                {
                    //get prof bonus from cr
                    int prof_bonus = helpers.helpers.GetProficiencyBonus(cr.cr);
                    //add proficiency bonus x amount of times
                    //where x is stored in the json
                    //then add dexmod
                    Initiative.initiative = (prof_bonus * Initiative.proficiency.Value) + dexmod;
                }

            }
        }

        //callback function: calculate 5etools link after deserialisation
        internal void calc_link()
        {
            this._5etools_link = helpers.helpers.build_link(Name, Source);
        }
    }


    public class hp {
        [JsonProperty("average")]
        public int average { get; set; }
        [JsonProperty("formula")]
        public string formula { get; set; }
    }

    public class ac_object : I_acItem
    {
        
        public int ac;
        public List<string> from { get; set; }

        public string condition { get; set; }
        public bool braces { get; set; }
        public int get_ac()
        {
            return ac;
        }
    }

    public class ac_int : I_acItem {
        public int ac; 

        public int get_ac() {
            return ac;
        }
        public ac_int(int ac) {
            this.ac = ac;
        }
    }

    //will have to calculate from dex score if not explicitly mentioned
    public class initiative_object {
        public int? initiative;

        public int? get_initiative() {
            return initiative;
        }

        public void set_initiative(int initiative) {
            this.initiative = initiative;
        }

        public int? proficiency { get; set; }

        //-1 for dis, 0 for normal, 1 for advantage
        public string advantageMode { get; set; }
        public int get_advantage() {
            if (string.IsNullOrEmpty(advantageMode))
            {
                return 0;
            }

            else if (advantageMode == "adv")
            {
                return 1;
            }
            else if (advantageMode == "dis")
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
    public class cr_object
    {

        public required double cr { get; set; }
        public string lair { get; set; }
        public string coven { get; set; }
        public int xp { get; set; }
        public int xpLair { get; set; }

    }

    public class special_object {
        public string special { get; set; }
    }

    public interface I_acItem {
        int get_ac();
    }

    
    //public interface I_initiative
    //{
    //    int get_initiative();
    //    int get_advantage();

    //}

}


