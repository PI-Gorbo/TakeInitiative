using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LiteDB;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TakeInitiative.Bestiary.Domain.JSON;

//https://github.com/TheGiddyLimit/homebrew/blob/master/_doc/Spec_Copy.md
namespace TakeInitiative.Bestiary.IngestionScript;

public static class NormaliseMonsters
{
    public static StopGapMonsterClass NormaliseMonster(LiteDatabase litedatabase, StopGapMonsterClass monster)
    {
        throw new NotImplementedException("This will be implemented soon");
        //First find the base mosnter to copy
        string copyname = monster.Copy.Name;
        string copysource = monster.Copy.Source;

    }
}
