//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.GridFS;
using System.Management.Automation;

namespace MongoDbGridFsProvider
{
    /// <summary>
    /// A Windows-Powershell DriveInfo to work with a MongoDb-GridFs.
    /// </summary>
    public class MongoDriveInfo : PSDriveInfo
    {
        private readonly string GridFsDefaultBucketName = "fs"; //default bucketname by mongodb

        public MongoProviderParameters DriveParameters { get; private set; }

        internal MongoDriveInfo(MongoProviderParameters driveParameters, PSDriveInfo drive) : base(drive)
        {
            DriveParameters = driveParameters;
            if (string.IsNullOrEmpty(DriveParameters.Collection))
            {
                DriveParameters.Collection = GridFsDefaultBucketName;
            }
        }

        internal MongoClient CreateMongoClient(MongoProvider provider)
        {
            var cxn = BuildMongoConnectionString(DriveParameters);
            return new MongoClient(cxn);
        }

        internal GridFSBucket GetGridFsBucket(MongoProvider provider)
        {
            var cxn = BuildMongoConnectionString(DriveParameters);
            var db = new MongoClient(cxn).GetDatabase(DriveParameters.Database);
            return new GridFSBucket(db, new GridFSBucketOptions
            {
                BucketName = DriveParameters.Collection
            });
        }

        private string BuildMongoConnectionString(MongoProviderParameters driveParameters, PSCredential credential = null)
        {
            var credentialString = "";
            if (credential != null)
            {
                credentialString = $"{credential.UserName}:{credential.Password}@";
            }

            var host = driveParameters.Host;
            var port = driveParameters.Port;

            var connectionString = $"mongodb://{credentialString}{host}:{port}";

            return new ConnectionString(connectionString).ToString();
        }

        internal void CheckConnection()
        {
            var cxn = BuildMongoConnectionString(DriveParameters);
            var db = new MongoClient(cxn);

            // Check if database exist
            if (!db.ListDatabaseNames().ToList().Contains(DriveParameters.Database))
            {
                throw new MongoException($"Database ' { DriveParameters.Database } ' not exists");
            }

            // Check if collection exist            
            if (!db.GetDatabase(DriveParameters.Database).ListCollectionNames().ToList().Contains(DriveParameters.Collection))
            {
                throw new MongoException($"Collection ' { DriveParameters.Collection } 'not exists");
            }
        }
    }
}