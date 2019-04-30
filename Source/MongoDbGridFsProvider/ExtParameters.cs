//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using System.Management.Automation;

namespace MongoDbGridFsProvider
{
    /// <summary>
    /// Class for extended PowerShell-Parameters for this provider.
    /// </summary>
    public class MongoProviderParameters
    {
        [Parameter(Mandatory = true)]
        public string Host { get; set; }

        [Parameter(Mandatory = true)]
        public string Database { get; set; }

        [Parameter(Mandatory = false)]
        public int Port { get; set; } = 27017;

        [Parameter(Mandatory = false)]
        public string Collection { get; set; }

        [Parameter]
        public SwitchParameter Verify { get; set; }
    }

    /// <summary>
    /// Class for extended PowerShell-Parameters for a mongo-item (file).
    /// </summary>
    public class MongoItemParameters
    {
        [Parameter(Mandatory = false)]
        public string Target { get; set; }

        [Parameter(Mandatory = false)]
        public string Name { get; set; }
    }
}