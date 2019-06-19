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
    public class MongoProvider : NavigationCmdletProvider, IContentCmdletProvider
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

        private GridFSBucket Bucket
        {
            get
            {
                return Drive.GetGridFsBucket(this);
            }
        }

        #endregion

        #region Private methods

        private static ObjectId UploadFile(GridFSBucket fs, string path)
        {
            var fileName = Path.GetFileName(path);
            var file = File.ReadAllBytes(Path.GetFullPath(path));
            return fs.UploadFromBytes(fileName, file);
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
            stream.Seek(0, SeekOrigin.Begin);

            using (var fileStream = File.Create(pathName))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }

            WriteToConsole(MessageType.Successful, $"Write file successfully: { pathName }");
        }

        private IEnumerable<GridFSFileInfo> GetFilesInfo(string fileName)
        {
            var items = new List<GridFSFileInfo>();
            if (!string.IsNullOrEmpty(fileName))
            {
                // Get file by name               
                items.AddRange(Bucket.Find(Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, fileName)).ToEnumerable());
            }

            return items;
        }

        private GridFSFileInfo GetFileInfo(ObjectId id)
        {
            var items = new List<GridFSFileInfo>();
            if (id != null)
            {
                // Get file by id
                items.AddRange(Bucket.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", id)).ToEnumerable());
            }

            if (items.Count() > 1)
            {
                // Multiple files found
                ThrowTerminatingError(new ErrorRecord(new ArgumentException($"File exists multiple times in the database."), "MultiIds", ErrorCategory.InvalidArgument, null));
            }

            return items.FirstOrDefault();
        }

        private void WriteGridFSFileInfoObject(GridFSFileInfo gridFSFileInfo, string path)
        {
            var outputObject = PSObject.AsPSObject(gridFSFileInfo);
            outputObject.Members.Add(new PSMemberSet("PSStandardMembers", new PSMemberInfo[]
            {
                new PSPropertySet("DefaultDisplayPropertySet", DefaultGridFsFileInfoProperties),
            }));
            WriteItemObject(gridFSFileInfo, path, false);
        }

        #endregion

        #region Overrides NavigationCmdletProvider

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

            p.Host = drive.Root;
            var joiner = Char.Parse(">");
            drive.CurrentLocation = $"{p.Database}{joiner}{p.Collection}";

            var mongoDrive = new MongoDriveInfo(p, drive);

            if (p.Verify.IsPresent)
            {
                // verify connection when drive is created.
                try
                {
                    mongoDrive.CheckConnection();
                }
                catch
                {
                    WriteToConsole(MessageType.Error, "Connection is not valid");
                    throw;
                }
            }

            return mongoDrive;
        }

        protected override bool ItemExists(string id)
        {
            id = RemovePathPrefix(id);

            if (string.IsNullOrEmpty(id))
            {
                return true;
            }

            try
            {
                var result = Bucket.Find(Builders<GridFSFileInfo>.Filter.Eq("_id", new ObjectId(id))).ToEnumerable();
                return result.Any();
            }
            catch
            {
                return false;
            }
        }

        protected override void GetItem(string id)
        {
            if (!(DynamicParameters is MongoItemParameters param))
            {
                throw new ArgumentException("Expected dynamic parameter of type " + typeof(MongoItemParameters).FullName);
            }

            id = RemovePathPrefix(id);
            var item = GetFileInfo(new ObjectId(id));
            WriteItemObject(item, item.Id.ToString(), false);

            if (!string.IsNullOrEmpty(param.Target))
            {
                //Load content to filesystem
                var stream = DownloadFile(Bucket, item.Id);
                var targetName = !string.IsNullOrEmpty(param.Target) ? param.Target : !string.IsNullOrEmpty(item.Filename) ? item.Filename.ToString() : item.Id.ToString();
                WriteFile(stream, targetName);
                stream.Close();
            }
        }

        protected override bool HasChildItems(string path)
        {
            return false; //Need false to enable 'RemoveItem' command.
        }

        protected override void RemoveItem(string path, bool recurse)
        {
            path = RemovePathPrefix(path);

            var desc = string.Format($"This will remove the document '{ path }' from collection '{ Drive.DriveParameters.Collection }' in database '{ Drive.DriveParameters.Database }'.");
            var warn = string.Format($"Do you want to remove document '{ path }' from collection '{ Drive.DriveParameters.Collection }' in database '{ Drive.DriveParameters.Database }'?");
            if (ShouldProcess(desc, warn, "Removing"))
            {
                if (Force || ShouldContinue(warn, "Confirm remove of document"))
                {
                    Bucket.Delete(new ObjectId(path));
                    WriteToConsole(MessageType.Successful, $"Document removed successfully");
                }
            }
        }

        protected override void RenameItem(string path, string newName)
        {
            Bucket.Rename(new ObjectId(path), newName);
            WriteToConsole(MessageType.Successful, $"Renaming successful");
        }

        protected override void SetItem(string path, object value)
        {
            path = RemovePathPrefix(path);
            if (File.Exists(path))
            {
                var id = UploadFile(Bucket, path);
                WriteItemObject(new PSObject(id), id.Pid.ToString(), false);
                WriteToConsole(MessageType.Successful, $"Upload successfully by Id '{ id }'");
            }
            else
            {
                WriteToConsole(MessageType.Error, $"File '{ path }' not found.");
            }
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            path = RemovePathPrefix(path);
            IEnumerable<GridFSFileInfo> result = Bucket.Find(FilterDefinition<GridFSFileInfo>.Empty).ToEnumerable(); // find any file

            foreach (var r in result)
            {
                WriteGridFSFileInfoObject(r, r.Filename);
            }
        }

        protected override object GetItemDynamicParameters(string path)
        {
            return new MongoItemParameters();
        }

        protected override bool IsItemContainer(string path)
        {
            path = RemovePathPrefix(path);
            return string.IsNullOrEmpty(path); //Only root-path should show children in collection
        }

        private string RemovePathPrefix(string path)
        {
            var root = Drive.Root + "\\";
            if (path.StartsWith(root))
            {
                path = path.Substring(root.Length);
            }

            var currentLocation = Drive.CurrentLocation;
            if (path.StartsWith(currentLocation))
            {
                path = path.Substring(currentLocation.Length);
            }

            return path;
        }

        protected override string[] ExpandPath(string path)
        {
            path = RemovePathPrefix(path);
            path = path.Replace('*', '.'); // Powershell wildcard (*) should be (.) in regex
            var regexPattern = $"^{path}.*";
            var result = Bucket.Find(Builders<GridFSFileInfo>.Filter.Regex(x => x.Filename, new BsonRegularExpression(regexPattern))).ToEnumerable();
            return result.Select(x => x.Id.ToString()).ToArray();
        }

        #endregion

        #region Override IContentCmdletProvider

        public IContentReader GetContentReader(string id)
        {
            if (!(DynamicParameters is MongoContentParameters param))
            {
                throw new ArgumentException("Expected dynamic parameter of type " + typeof(MongoContentParameters).FullName);
            }

            id = RemovePathPrefix(id);
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(param.Name))
            {
                GridFSFileInfo newestFile = null;
                foreach (var entry in GetFilesInfo(param.Name).ToList())
                {
                    if (newestFile == null)
                    {
                        newestFile = entry;
                    }
                    else
                    {
                        if (entry.UploadDateTime.Ticks > newestFile.UploadDateTime.Ticks)
                        {
                            newestFile = entry;
                        }
                    }
                }

                id = newestFile.Id.ToString();
            }

            return new MongoReadContentProvider(Bucket, new ObjectId(id));
        }

        public object GetContentReaderDynamicParameters(string path)
        {
            return new MongoContentParameters();
        }

        public IContentWriter GetContentWriter(string id)
        {
            if (!(DynamicParameters is MongoContentParameters param))
            {
                throw new ArgumentException("Expected dynamic parameter of type " + typeof(MongoContentParameters).FullName);
            }

            id = RemovePathPrefix(id);
            if (!string.IsNullOrEmpty(id))
            {
                ThrowTerminatingError(new ErrorRecord(new InvalidOperationException($"MongoDb does not support overwrite a gridfs-entry. Delete existing element first."), "FileAlreadyExist", ErrorCategory.InvalidArgument, null));
            }

            return new MongoWriteContentProvider(Bucket, param.Name);
        }

        public object GetContentWriterDynamicParameters(string path)
        {
            return new MongoContentParameters();
        }

        public void ClearContent(string path)
        {
            // Nothing to do
        }

        public object ClearContentDynamicParameters(string path)
        {
            // Default implementation
            return null;
        }

        #endregion
    }
}