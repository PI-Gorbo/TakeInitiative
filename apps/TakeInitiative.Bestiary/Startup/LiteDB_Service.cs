using System;

using FastEndpoints;

using LiteDB;

using Microsoft.Extensions.Options;
namespace TakeInitiative.BestiaryAPI.Startup
{
    public class LiteDBContext
    {
        public readonly LiteDatabase litedb;

        public LiteDBContext(string dbpath)
        {
            try
            {
                var db = new LiteDatabase(dbpath);
                if (db != null)
                    litedb = db;
            }
            catch (Exception ex)
            {
                throw new Exception("Can't find or create LiteDb database.", ex);
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
}
