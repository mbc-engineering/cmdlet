//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using static MongoDbGridFsProvider.ConsoleMessage;

namespace MongoDbGridFsProvider
{
    /// <summary>
    /// Class represents a Windows-Powershell provider to handle a GridFs (Filesystem) from a MongoDb Database.
    /// </summary>
    [CmdletProvider("MongoDbGridFs", ProviderCapabilities.Filter | ProviderCapabilities.ShouldProcess | ProviderCapabilities.Credentials | ProviderCapabilities.ExpandWildcards)]
    public class MongoProvider : NavigationCmdletProvider
    {
        public static IReadOnlyList<string> DefaultGridFsFileInfoProperties = new List<string>
        {
            "FileName", "Id", "UploadDateTime", "length"
        }.AsReadOnly();

        #region Property

        internal MongoDriveInfo Drive
        {
            get
            {
                var drive = PSDriveInfo as MongoDriveInfo;

                if (drive == null)
                {
                    drive = ProviderInfo.Drives.FirstOrDefault() as MongoDriveInfo;
                }

                return drive;
            }
        }

        #endregion

        #region Private methods

        private static ObjectId UploadFile(GridFSBucket fs, string path)
        {
            var fileName = Path.GetFileName(path);
            using (var stream = File.OpenRead(path))
            {
                return fs.UploadFromStream(fileName, stream);
            }
        }

        private static Stream DownloadFile(GridFSBucket fs, ObjectId id)
        {
            var stream = new MemoryStream();
            fs.DownloadToStream(id, stream);
            return stream;
        }

        private void WriteFile(Stream stream, string pathName)
        {
            if (File.Exists(pathName))
            {
                bool yesToAll = false, noToAll = false;
                if (!ShouldContinue($"File '{ pathName }' exists already, should it overwrite?", "File already exists", ref yesToAll, ref noToAll))
                {
                    return;
                }
            }

            using (var fileStream = File.Create(pathName))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }

            WriteToConsole(MessageType.Successful, $"Write file successfully: { pathName }");
        }

        #endregion

        #region Overrides

        protected override bool IsValidPath(string path)
        {
            return true;
        }

        protected override object NewDriveDynamicParameters()
        {
            return new MongoProviderParameters();
        }

        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            if (!(DynamicParameters is MongoProviderParameters p))
            {
                throw new ArgumentException("Expected dynamic parameter of type " + typeof(MongoProviderParameters).FullName);
            }

            var mongoDrive = new MongoDriveInfo(p, drive);

            if (p.Verify.IsPresent)
            {
                // verify connection when drive is created.
                var client = mongoDrive.CreateMongoClient(this);
                try
                {
                    client.GetDatabase(p.Database);
                }
                catch (Exception)
                {
                    WriteInformation(new InformationRecord("A no connection", "B no connection"));
                }
            }

            return mongoDrive;
        }

        protected override bool ItemExists(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return true;
            }
            var fs = Drive.GetGridFsBucket(this);
            var result = fs.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", new ObjectId(id))).ToEnumerable();

            return result.Any();
        }

        protected override void GetItem(string path)
        {
            if (!(DynamicParameters is MongoItemParameters param))
            {
                throw new ArgumentException("Expected dynamic parameter of type " + typeof(MongoItemParameters).FullName);
            }

            var fs = Drive.GetGridFsBucket(this);

            var items = new List<GridFSFileInfo>();
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(param.Name))
            {
                // Get file by name               
                items.AddRange(fs.Find(Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, param.Name)).ToEnumerable());
            }
            else
            {
                // Get file by id
                items.AddRange(fs.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", new ObjectId(path))).ToEnumerable());
            }

            if (!items.Any())
            {
                // File not found
                ThrowTerminatingError(new ErrorRecord(new FileNotFoundException($"File '{path}' not found in database"), "FileNotFound", ErrorCategory.ObjectNotFound, null));
            }

            if (items.Count() != 1)
            {
                // Multiple files found
                ThrowTerminatingError(new ErrorRecord(new ArgumentException($"File exists multiple times in the database. Use the unique ID property for specificatioin."), "MultiNames", ErrorCategory.InvalidArgument, null));
            }

            var item = items.First();
            WriteItemObject(item, item.Id.ToString(), false);

            if (!string.IsNullOrEmpty(param.Target))
            {
                //Load content to filesystem
                var stream = DownloadFile(fs, item.Id);
                var targetName = !string.IsNullOrEmpty(param.Target) ? param.Target : !string.IsNullOrEmpty(item.Filename) ? item.Filename.ToString() : item.Id.ToString();
                WriteFile(stream, targetName);
            }
        }

        protected override bool HasChildItems(string path)
        {
            return false; //Need false to enable 'RemoveItem' command.
        }

        protected override void RemoveItem(string path, bool recurse)
        {
            var desc = string.Format($"This will remove the document '{ path }' from collection '{ Drive.DriveParameters.Collection }' in database '{ Drive.DriveParameters.Database }'.");
            var warn = string.Format($"Do you want to remove document '{ path }' from collection '{ Drive.DriveParameters.Collection }' in database '{ Drive.DriveParameters.Database }'?");
            if (ShouldProcess(desc, warn, "Removing"))
            {
                if (Force || ShouldContinue(warn, "Confirm remove of document"))
                {
                    var fs = Drive.GetGridFsBucket(this);
                    fs.Delete(new ObjectId(path));
                    WriteToConsole(MessageType.Successful, $"Document removed successfully");
                }
            }
        }

        protected override void RenameItem(string path, string newName)
        {
            var fs = Drive.GetGridFsBucket(this);
            fs.Rename(new ObjectId(path), newName);
            WriteToConsole(MessageType.Successful, $"Renaming successful");
        }

        protected override void SetItem(string path, object value)
        {
            if (File.Exists(path))
            {
                var fs = Drive.GetGridFsBucket(this);
                var id = UploadFile(fs, path);
                WriteItemObject(new PSObject(id), id.Pid.ToString(), false);
                WriteToConsole(MessageType.Successful, $"Upload successfully by Id '{ id }'");
            }
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            var fs = Drive.GetGridFsBucket(this);
            IEnumerable<GridFSFileInfo> result = fs.Find(FilterDefinition<GridFSFileInfo>.Empty).ToEnumerable(); // find any file

            foreach (var r in result)
            {
                WriteGridFSFileInfoObject(r, r.Filename);
            }
        }

        private void WriteGridFSFileInfoObject(GridFSFileInfo gridFSFileInfo, string path)
        {
            var outputObject = PSObject.AsPSObject(gridFSFileInfo);
            outputObject.Members.Add(new PSMemberSet("PSStandardMembers", new PSMemberInfo[]
            {
                new PSPropertySet("DefaultDisplayPropertySet", DefaultGridFsFileInfoProperties),
            }));
            WriteItemObject(outputObject, path, false);
        }

        protected override object GetItemDynamicParameters(string path)
        {
            return new MongoItemParameters();
        }

        protected override bool IsItemContainer(string path)
        {
            return string.IsNullOrEmpty(path); //Only root-path should show children in collection
        }

        protected override string[] ExpandPath(string path)
        {
            var fs = Drive.GetGridFsBucket(this);
            path = path.Replace('*', '.'); // Powershell wildcard (*) should be (.) in regex
            var regexPattern = $"^{path}.*";
            var result = fs.Find(Builders<GridFSFileInfo>.Filter.Regex(x => x.Filename, new BsonRegularExpression(regexPattern))).ToEnumerable();
            return result.Select(x => x.Id.ToString()).ToArray();
        }

        #endregion
    }
}