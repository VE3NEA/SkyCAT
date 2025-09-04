using CommandLine;
using SkyCat;
using System;

namespace skycatd
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Parser.Default.ParseArguments<Options>(args)
          .WithParsed(Run)
          .WithNotParsed(errors => { Environment.Exit(1); }
          );
    }

    private static void Run(Options options)
    {
      if (!options.Validate()) Environment.Exit(1);

      var version = typeof(Program).Assembly.GetName().Version;

      if (options.List)
        Console.WriteLine(new CatCommandSender().ListModels());

      else if (options.All)
        Console.WriteLine(new CatCommandSender().ListAllCapabilities());

      else
      {
        CatServer? server = null;
        Console.WriteLine($"skycatd started.");
        Console.WriteLine($"Log level: {options.LogLevel}");
        Console.WriteLine("Press Ctrl-C to exit.\n");
        try
        {
          server = new CatServer(options);
        }
        catch (Exception ex)
        {
          Console.Error.WriteLine($"Error creating server: {ex.Message}");
          Environment.Exit(1);
        }
        server.Run();
      }
    }
  }
}