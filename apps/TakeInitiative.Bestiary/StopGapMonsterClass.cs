using Microsoft.AspNetCore.Routing.Constraints;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BestiaryAPI

{

    public class MonsterConverter : JsonConverter {
        #region boilerplate jsonconverter stuff
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Not implemented yet");
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
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        
        }


    }


    public class root {

        [JsonProperty("monster")]
        public List<StopGapMonsterClass> Monsters { get; init; }
    }
    public class StopGapMonsterClass
    {

        [JsonProperty("hp")]
        public hp Hp { get; init; }
        [JsonProperty("ac")]
        public I_acItem Ac { get; init; }
        [JsonProperty("name")]
        public string Name{ get; init; }
        [JsonProperty("source")]
        public string Source { get; init; }

        public I_initiative Initiative { get; init; }

    }

    
    public class hp {
        [JsonProperty("average")]
        public int average { get; init; }
        [JsonProperty("formula")]
        public string formula { get; init; }
    }

    public class ac_object : I_acItem
    {
        
        public int ac;
        public List<string> from { get; init; }

        public string condition { get; init; }
        public bool braces { get; init; }
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
    }

    //will have to calculate from dex score if not explicitly mentioned
    public class initiative_object : I_initiative {
        public int initiative;

        public int get_initiative() {
            return initiative;
        }


        //-1 for dis, 0 for normal, 1 for advantage
        public int advantage;
        public int get_advantage() {
            return advantage;
        }


    }



    public class special_object {
        public string special { get; init; }
    }

    public interface I_acItem {
        int get_ac();
    }

    
    public interface I_initiative
    {
        int get_initiative();
        int get_advantage();

    }

}


