using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using Xunit;

namespace MongoDbGridFsProvider.Test
{
    public class MongoGridFsProviderTest : IDisposable
    {
        private MongoDbRunner _runner;
        private MongoClientSettings _mongoClientSettings;
        private MongoClient _client;
        private IMongoDatabase _db;

        public MongoGridFsProviderTest()
        {
            _runner = MongoDbRunner.Start();
            _mongoClientSettings = MongoClientSettings.FromConnectionString(_runner.ConnectionString);

            _client = new MongoClient(_mongoClientSettings);
            _db = _client.GetDatabase("foo");
        }

        public void Dispose()
        {
            _runner.Dispose();
        }

        private PowerShell CreatePowerShell()
        {
            var iss = InitialSessionState.CreateDefault();
            iss.Providers.Add(new SessionStateProviderEntry("MongoDbGridFs", typeof(MongoProvider), ""));

            var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();

            var ps = PowerShell.Create();
            ps.Runspace = rs;

            ps.AddCommand("New-PSDrive")
                .AddParameter("Name", "mongodb")
                .AddParameter("PSProvider", "MongoDbGridFs")
                .AddParameter("Root", "localhost")
                .AddParameter("Port", _mongoClientSettings.Server.Port.ToString())
                .AddParameter("Database", "foo")
                .AddParameter("Collection", "")
                .AddCommand("Out-Null");
            ps.AddStatement();

            return ps;
        }

        [Fact]
        public void TestProviderRegistration()
        {
            var ps = CreatePowerShell();

            var result = ps.AddCommand("Get-PSProvider").Invoke();

            result.Should().AllBeOfType<PSObject>().And.Contain(x => ((ProviderInfo)x.ImmediateBaseObject).Name == "MongoDbGridFs");
        }

        [Fact]
        public void TestDriveRegistration()
        {
            var ps = CreatePowerShell();

            var result = ps.AddCommand("Get-PSDrive").Invoke();

            result.Should().AllBeOfType<PSObject>().And.Contain(x => ((PSDriveInfo)x.ImmediateBaseObject).Name == "mongodb");
        }

        [Fact]
        public void TestEmptyGridFS()
        {
            // Arrange
            var ps = CreatePowerShell();

            // Act
            var result = ps.AddCommand("Get-Childitem")
                .AddParameter("Path", "mongodb:")
                .Invoke();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void TestGetChildItem()
        {
            // Arrange
            var bucket = new GridFSBucket(_db);
            var id1 = bucket.UploadFromBytes("a", new byte[1]);
            var id2 = bucket.UploadFromBytes("b", new byte[1]);
            var id3 = bucket.UploadFromBytes(ObjectId.GenerateNewId().ToString(), new byte[1]);
            var ps = CreatePowerShell();

            // Act
            var result = ps.AddCommand("Get-ChildItem")
                .AddParameter("Path", "mongodb:")
                .Invoke();

            // Assert
            result.Should().HaveCount(3);

            result.Cast<PSObject>()
                .Select(x => x.ImmediateBaseObject)
                .Should().AllBeOfType<GridFSFileInfo>()
                .And.Subject.Cast<GridFSFileInfo>().Select(x => x.Id).Should().BeEquivalentTo(id1, id2, id3);
        }

        [Fact]
        public void TestSetContent()
        {
            // Arrange
            var ps = CreatePowerShell();
            var bucket = new GridFSBucket(_db);

            // Act
            ps.AddCommand("Set-Content")
                .AddParameter("Path", "mongodb:")
                .AddParameter("Name", "abc")
                .AddParameter("Value", "foo")
                .Invoke();

            // Assert
            bucket.DownloadAsBytesByName("abc").Should().BeEquivalentTo(Encoding.ASCII.GetBytes("foo"));
        }
    }
}
