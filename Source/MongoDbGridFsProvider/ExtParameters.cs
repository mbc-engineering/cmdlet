using System.Management.Automation;

namespace MongoDbGridFsProvider
{
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

    public class MongoItemParameters
    {
        [Parameter(Mandatory = false)]
        public string Target { get; set; }

        [Parameter(Mandatory = false)]
        public string Name { get; set; }
    }
}
