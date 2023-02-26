// See https://aka.ms/new-console-template for more information
using ImageMagick;
//using System.Runtime.CompilerServices;
//using static System.Net.Mime.MediaTypeNames;
using ImproveTransparentPixels;

Console.WriteLine("Hello, World!");
Console.WriteLine(string.Join(", ", args));

if (ParseParameters(args, out string inputFilePath, out string outputFilePath, out int maxDistance, out bool runFast))
{
    try
    {
        MagickNET.Initialize();
        using MagickImage image = new MagickImage(inputFilePath);
        Processor processor = new Processor(image);
        MagickImage improvedImage = processor.ImproveTransparentPixels(maxDistance, runFast);
        improvedImage.Write(outputFilePath);
        return 0;
    }
    finally{ }
 //   catch(Exception e)
 //   {
 //       System.Console.WriteLine("Encountered an error during processing - " + e.Message);
 //   }
}
return -1;

bool ParseParameters(string[] args, out string inputFilePath, out string outputFilePath, out int maxDistance, out bool runFast)
{
    inputFilePath = "";
    outputFilePath = "";
    maxDistance = int.MaxValue;
    runFast = false;

    foreach (var arg in args)
    {
        if (arg.StartsWith("-"))
        {
            if (arg.ToLower() == "-fast")
            {
                runFast = true;
            }
            else if (arg.ToLower() == "-slow")
            {
                runFast = false;
            }
            else if (int.TryParse(arg.Substring(1), out int newMaxDistance))
            {
                maxDistance = newMaxDistance;
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

    return true;
}

void UnhandledArgument(string message)
{
    System.Console.WriteLine(message);
    System.Console.WriteLine("Expected use is:");
    System.Console.WriteLine("ImproveTransparentPixels sourcefile.png [-MaxDistance] [slow|fast]");
}


string GetDefaultOutputFilePath(string inputFilePath) => Path.ChangeExtension(inputFilePath, ".ImprovedTransparent.png");


