//-----------------------------------------------------------------------------
// Copyright (c) 2019 by mbc engineering GmbH, CH-6015 Luzern
// Licensed under the Apache License, Version 2.0
//-----------------------------------------------------------------------------

using System;

namespace MongoDbGridFsProvider
{
    /// <summary>
    /// Class to handle messages to the powershell-console.
    /// </summary>
    internal class ConsoleMessage
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