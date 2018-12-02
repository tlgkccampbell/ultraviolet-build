using System;
using CommandLine;

namespace UvProj.Options
{
    [Verb("new", HelpText = "Creates a new Ultraviolet project for all supported platforms.")]
    public class NewOptions
    {
        [Option('n', "name", HelpText = "The name of the created project.", Required = true)]
        public String Name { get; set; }
    }
}
