using MongoDB.Driver;
using System;
using System.Text;

namespace MongoDbGridFsProvider
{
    class Context
    {
        public MongoProvider Provider { get; private set; }
        public object DynamicParameters { get; private set; }
        public MongoClient MongoClient { get; private set; }

        public Context(MongoProvider provider, object dynamicParameters, MongoClient client)
        {
            Provider = provider;
            DynamicParameters = dynamicParameters;
            MongoClient = client;
        }

    }

    static class Paths
    {
        internal static string Create(Context context, string databaseName, string collection)
        {
            return Create(context.Provider.Drive.Name, databaseName, collection);
        }

        static string Create(string driveName, string databaseName, string collectionName)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("{0}:/", driveName);
            if (null != databaseName)
            {
                builder.AppendFormat("/{0}", databaseName);
            }
            if (null != collectionName)
            {
                builder.AppendFormat("/{0}", collectionName);
            }
            
            return builder.ToString();
        }
    }
}
