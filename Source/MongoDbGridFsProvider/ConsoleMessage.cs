using System;

namespace MongoDbGridFsProvider
{
    class ConsoleMessage
    {
        public enum MessageType
        {
            Successful,
            Info,
            Error,
        }
        internal static void WriteToConsole(MessageType type, string message)
        {
            switch (type)
            {
                case MessageType.Successful:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case MessageType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }

            Console.WriteLine(message, new object[0]);
            Console.ResetColor(); // reset to the default color
        }
    }
}
