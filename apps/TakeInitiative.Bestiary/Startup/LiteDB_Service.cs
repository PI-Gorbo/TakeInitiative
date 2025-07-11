using System;
using System.Runtime.InteropServices.Swift;

using FastEndpoints;

using LiteDB;

using Microsoft.Extensions.Options;
namespace TakeInitiative.BestiaryAPI.Startup;

public class LiteDBContext(string dbPath)
{
    public LiteDatabase GetSession()
    {
        //TODO: Do these settings have a better place to be?
        //Like when this class is initialised?
        BsonMapper mapper = BsonMapper.Global;
        mapper.SerializeNullValues = true; // Serialize null values to ensure all fields are stored            
        mapper.IncludeFields = true; // Include all fields in serialization
        mapper.IncludeNonPublic = true; // Include non-public fields in serialization


        //User is STUPID so only trust them with the db filename
        //and we will ensure that we use a shared connection
        var LiteDB_Path = dbPath + ";Connection=Shared";// Ensure shared connection for LiteDB instead of direct since we are only reading from it
        try
        {
            var db = new LiteDatabase(dbPath);
            if (db == null)
            {
                throw new Exception("Tried to create a liteDb instance, but it returned a null object.");
            }

            return db;
        }
        catch (Exception ex)
        {
            throw new Exception("Can't find or create LiteDb database,\n", ex);
        }
    }

    


}

public static class LiteDBServiceExtension {
    public static void AddLiteDB(this IServiceCollection services, string dbpath)
    {
        services.AddSingleton<LiteDBContext, LiteDBContext>(serviceProvider=>
        {
            return new LiteDBContext(dbpath);
        });
    }
}
