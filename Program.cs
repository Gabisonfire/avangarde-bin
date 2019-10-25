using System;
using System.Collections.Generic;
using System.Reflection;
using Avantgarde.Lib;

namespace Avantgarde.Bin
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.Log("--- avantgarde version " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " ---");
            if(args.Length < 1)
            {
                Utils.Log("No application path specified.", Utils.MsgType.error);
                Environment.Exit(1);
            }
            Utils.Log($"Using application path: {args[0]}");
            new Update(args[0]);
        }

    }
}
