﻿// See https://aka.ms/new-console-template for more information
using ImageMagick;
//using System.Runtime.CompilerServices;
//using static System.Net.Mime.MediaTypeNames;
using ImproveTransparentPixels;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

Console.WriteLine("Hello, World!");
Console.WriteLine(string.Join(", ", args));

if (ParseParameters(args, out string inputFilePath, out string outputFilePath, out List<Operation> operations))
{
    try
    {
        MagickNET.Initialize();
        using MagickImage image = new MagickImage(inputFilePath);
        Processor processor = new Processor(image);
        foreach (Operation operation in operations)
        {
            operation.DoOperation(processor);
        }
        processor.GetOutput().Write(outputFilePath);
        return 0;
    }
    catch (NullReferenceException e)
    {
        System.Console.WriteLine("Encountered an error during processing - " + e.Message);
    }
}
return -1;






bool ParseParameters(string[] args, out string inputFilePath, out string outputFilePath, out List<Operation> operations)
{
    inputFilePath = "";
    outputFilePath = "";
    operations = new List<Operation>();

    Regex solidifyRegex = new Regex(@"^-solidify(:(?<MaxDistance>\d+))?$", RegexOptions.IgnoreCase);

    foreach (var arg in args)
    {
        if (arg.StartsWith("-"))
        {
            Match match;
            if ( (match = solidifyRegex.Match(arg)) is { Success:true } )
            {
                var solidify = new SolidifyOperation();
                if (match.Groups.ContainsKey("MaxDistance"))
                    solidify.MaxDistance = int.Parse(  match.Groups["MaxDistance"].Value);
                operations.Add(solidify);
            }
            else
            {
                UnhandledArgument("Unhandled argument " + arg);
                return false;
            }
        }
        else if (File.Exists(arg))
            inputFilePath = arg;
        else
        {
            UnhandledArgument("Could not find input file " + arg);
            return false;
        }
    }

    if (string.IsNullOrEmpty(inputFilePath))
    {
        UnhandledArgument("No input file specified");
        return false;
    }
    if (string.IsNullOrEmpty(outputFilePath))
    {
        outputFilePath = GetDefaultOutputFilePath(inputFilePath);
    }
    if (operations.Count == 0)
    {
        operations.Add(new SolidifyOperation());
    }

    return true;
}

void UnhandledArgument(string message)
{
    System.Console.WriteLine(message);
    System.Console.WriteLine("Expected use is on of the following:");
    System.Console.WriteLine("ImproveTransparentPixels sourcefile [-operations]");
    System.Console.WriteLine("ImproveTransparentPixels -in:sourcefile -out:outputFile [-operations]");
    System.Console.WriteLine("Valid operations are:");
    System.Console.WriteLine("-solidify[:maxDistance]");
    System.Console.WriteLine("-color[:colorhex]");
    System.Console.WriteLine("If you list multiple operations, they will run in order until all pixels have been processed.");
    System.Console.WriteLine("If no operation is specified, then -solidify is the default operation");
    System.Console.WriteLine("See the README.md file for more information");
}


string GetDefaultOutputFilePath(string inputFilePath) => Path.ChangeExtension(inputFilePath, ".ImprovedTransparent.png");

