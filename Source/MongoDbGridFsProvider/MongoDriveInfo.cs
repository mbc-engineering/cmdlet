using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.GridFS;
using System.Management.Automation;

namespace MongoDbGridFsProvider
{
    internal class MongoDriveInfo : PSDriveInfo
    {
        public MongoProviderParameters DriveParameters { get; private set; }
        private readonly string GridFsDefaultBucketName = "fs"; //default bucketname by mongodb

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
            var client = new MongoClient(cxn);
            var db = client.GetDatabase(DriveParameters.Database);
            return new MongoClient(cxn);
        }

        internal GridFSBucket GetGridFsBucket(MongoProvider provider)
        {
            var cxn = BuildMongoConnectionString(DriveParameters);
            var db = new MongoClient(cxn).GetDatabase(DriveParameters.Database);
            return new GridFSBucket(db, new GridFSBucketOptions {
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
    }
}