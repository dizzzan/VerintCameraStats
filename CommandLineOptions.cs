
using System.Net;
using CommandLine;

public class CommandLineOptions
    {
        [Option(shortName: 'c', longName: "connstring", Required = true, HelpText = "Connection String")]
        public string ConnString { get; set; }

        [Option(shortName: 'm', longName: "machine", Required = false, HelpText = "Override machine name")]
        public string Machine { get; set; }

        [Option(shortName: 'd', longName: "days", Required = false, HelpText = "Look back number of days", Default = 30)]
        public int Days { get; set; }

         [Option(shortName: 'p', longName: "path", Required = false, HelpText = "Path to video folders relative to base device", Default = "Verint\\CCTVWare")]
        public string Path { get; set; }
    }