//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using MongoDB.Driver;
using System.Text;

namespace MongoDbGridFsProvider
{
    /// <summary>
    /// Class to handle powershell context.
    /// </summary>
    internal class Context
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

    /// <summary>
    /// Helper-class to build a pathes.
    /// </summary>
    internal static class Paths
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