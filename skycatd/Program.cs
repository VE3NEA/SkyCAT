using CommandLine;
using SkyCat;
using System;
using System.Runtime.InteropServices;

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

        // on Ctrl-C, closing the console window, logoff and shutdown, stop the server gracefully
        // so that it tears down the radio before the process exits
        using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => StopOnSignal(server!, ctx));
        using var sigQuit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, ctx => StopOnSignal(server!, ctx));
        using var sigHup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, ctx => StopOnSignal(server!, ctx));
        using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => StopOnSignal(server!, ctx));

        server.Run();
      }
    }

    private static void StopOnSignal(CatServer server, PosixSignalContext context)
    {
      context.Cancel = true;
      server.Stop();

      // when the console window is closed, Windows ends the process as soon as this handler returns,
      // and allows about 5 seconds for it, so wait here until the teardown is complete
      server.WaitUntilStopped(4000);
    }
  }
}