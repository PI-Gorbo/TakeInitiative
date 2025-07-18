using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LiteDB;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TakeInitiative.Bestiary.Domain.JSON;
using TakeInitiative.Bestiary.Domain.JSON.Copy;
//https://github.com/TheGiddyLimit/homebrew/blob/master/_doc/Spec_Copy.md
namespace TakeInitiative.Bestiary.IngestionScript;

public static class NormaliseMonsters
{
    public static StopGapMonsterClass NormaliseMonster(LiteDatabase litedatabase, StopGapMonsterClass monster)
    {

        //Debug.WriteLine("NAME IS " + monster.Name);
        
        //First find the base mosnter to copy
        string copyname = monster.Copy.Name;
        string copysource = monster.Copy.Source;
        
        

        var collection = litedatabase.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
        var monsters = collection.Query()
            .Where(x => x.Name.Equals(copyname) && x.Source.Equals(copysource))
            .ToList();
        if (monsters.Count != 1)
        {
            throw new Exception($"Expected to find exactly one monster with name '{copyname}' and source '{copysource}', but found {monsters.Count}.");
        }
        StopGapMonsterClass refMonster = monsters.First();
        //TODO: Monster doesnt actually have fields to modify yet
        //For now we just copy over AC, dex, hp, initiative and cr
        //otherwise we should copy things over last
        //after we've applied all the text and such modifiers to the reference monster
        CopyMonsterFields(ref monster, refMonster);
        return monster;

    }


    public static void CopyMonsterFields(ref StopGapMonsterClass monster, StopGapMonsterClass refMonster)
    {
        monster.Ac = refMonster.Ac;
        monster.Hp = refMonster.Hp;
        monster.Initiative = refMonster.Initiative;
        monster.Dex = refMonster.Dex;
        monster.Cr = refMonster.Cr;
    }
    

    public static void HandleGlobalMods(ref StopGapMonsterClass monster, StopGapMonsterClass refMonster)
    {
        var globalMod = monster.Copy.Mod.GlobalMods;
        //TODO: The StopGapMonsterClass does not have fields that are relevant for copy modding yet 
        //eg global mods apply to "action", "reaction", "trait", "legendary", "variant", and "spellcasting"

    }

    /// <summary>
    /// Here the monster parameter is the monster being "copied" and replace must be of type ReplaceTxt
    /// </summary>
    /// <param name="monster"></param>
    /// <param name="replace"></param>
    public static void ApplyReplaceTxtMod(ref StopGapMonsterClass monster, ICopyModifier replace)
    {
        //TODO
    }
}
