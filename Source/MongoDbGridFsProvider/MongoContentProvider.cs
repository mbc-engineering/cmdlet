//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Provider;
using System.Text;

namespace MongoDbGridFsProvider
{
    public class MongoReadContentProvider : IContentReader
    {
        private readonly GridFSDownloadStream stream;

        public MongoReadContentProvider(GridFSBucket bucket, ObjectId id)
        {
            this.stream = bucket.OpenDownloadStream(id);
        }

        public IList Read(long readCount)
        {
            if (stream.Position == stream.Length)
            {
                return null;
            }

            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            return new List<object>() { Encoding.UTF8.GetString(memoryStream.ToArray()) };
        }

        public void Close() => stream.Close();
        public void Dispose() => stream.Close();
        public void Seek(long offset, SeekOrigin origin) { }
    }

    public class MongoWriteContentProvider : IContentWriter
    {
        private readonly GridFSUploadStream stream;

        public MongoWriteContentProvider(GridFSBucket bucket, string label)
        {
            this.stream = bucket.OpenUploadStream(label);
        }

        public IList Write(IList content)
        {
            if (content is object[] o)
            {
                var bin = Encoding.UTF8.GetBytes(o[0].ToString());
                stream.Write(bin, 0, bin.Count());
            }
            return content;
        }

        public void Close() => stream.Close();
        public void Dispose() => stream.Close();
        public void Seek(long offset, SeekOrigin origin) { }
    }
}