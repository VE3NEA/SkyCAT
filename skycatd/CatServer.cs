using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using SkyCat;

namespace skycatd
{

  public class CatServer
  {
    // log level of a message will be selected based on the port status
    private enum PortStatus { NeverOpened, WasOpen, WasClosed }

    private readonly Options options;
    private readonly CancellationTokenSource cts = new();
    private readonly Microsoft.Extensions.Logging.ILogger logger;
    private readonly TcpServer tcpServer;
    private readonly CommandInterpreter commandInterpreter;
    private readonly CatCommandSender commandSender;
    private readonly SerialPort serialPort;

    private PortStatus ComStatus;
    private PortStatus TcpStatus;

    public CatServer(Options options)
    {
      this.options = options;

      logger = CreateLogger(options);

      var version = typeof(Program).Assembly.GetName().Version;
      logger.LogInformation($"Starting CAT server: skycatd v.{version}.");

      commandInterpreter = new CommandInterpreter(options, logger);
      commandSender = commandInterpreter.CommandSender;
      serialPort = commandSender.SerialPort;

      tcpServer = new TcpServer(options.Port, commandInterpreter, logger);
    }

    ConsoleTheme theme = new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
    {
      [ConsoleThemeStyle.LevelFatal] = "\x1b[41m\x1b[37m", // white on red background
      [ConsoleThemeStyle.LevelError] = "\x1b[1;31m",       // red
      [ConsoleThemeStyle.LevelWarning] = "\x1b[1;33m",     // yellow
      [ConsoleThemeStyle.LevelInformation] = "\x1b[1;32m", // green
      [ConsoleThemeStyle.LevelDebug] = "\x1b[1;37m",       // white
      [ConsoleThemeStyle.LevelVerbose] = "\x1b[37m",       // gray
      [ConsoleThemeStyle.Text] = "\x1b[37m",               // gray
    });

    private Microsoft.Extensions.Logging.ILogger CreateLogger(Options options)
    {
      var loggerConfig = new LoggerConfiguration()
        .MinimumLevel.Is(options.LogLevel)
        .WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            theme: theme
        );

      if (options.FileLog)
      {
        string logFilePath = $"Logs\\skycatd_{DateTime.Now:yyyy-MM-dd_HHmmss}.log";

        loggerConfig = loggerConfig.WriteTo.File(
            logFilePath,
            outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            rollingInterval: RollingInterval.Infinite,
            shared: true
        );

        Console.WriteLine($"Log file created at: {Path.GetFullPath(logFilePath)}\n");
      }

      Log.Logger = loggerConfig.CreateLogger();
      var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);
      return loggerFactory.CreateLogger(typeof(CatServer).FullName ?? nameof(CatServer));
    }

    private void SleepWithCancellation(int totalMilliseconds)
    {
        const int interval = 100; 
        int elapsed = 0;
        
        while (elapsed < totalMilliseconds && !cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(interval);
            elapsed += interval;
        }
    }

    public void Run()
    {
      while (!cts.Token.IsCancellationRequested)
      {
        if (!serialPort.IsOpen)
          // try to open com port
          try
          {
            if (ComStatus == PortStatus.WasOpen) logger.LogWarning($"Serial port closed unexpectedly. Reopening.");
            else if (ComStatus == PortStatus.NeverOpened) logger.LogInformation($"Opening serial port {options.RigFile} at {serialPort.BaudRate} Baud...");
            else logger.LogTrace("Opening serial port...");

            serialPort.Open();
            ComStatus = PortStatus.WasOpen;
            Thread.Sleep(300);

            logger.LogInformation("Serial port opened.");
            logger.LogInformation($"Starting TCP server on port {options.Port}...");
          }
          catch (Exception ex)
          {
            string message = $"Failed to open serial port: {ex.Message} Will retry.";
            if (ComStatus == PortStatus.WasClosed) logger.LogTrace(message); else logger.LogWarning(message);
            tcpServer.Stop();
            ComStatus = PortStatus.WasClosed;
            TcpStatus = PortStatus.WasClosed;
          }

        // if com is open, try to start tcp server
        if (serialPort.IsOpen && !tcpServer.IsListening())
          try
          {
            if (TcpStatus == PortStatus.WasOpen) logger.LogInformation($"TCP server stopped unexpectedly. Restarting."); 
            tcpServer.Start();
            TcpStatus = PortStatus.WasOpen;
            logger.LogInformation($"TCP server started.");
          }
          catch (Exception ex)
          {
            string message = $"Failed to start TCP server: {ex.Message} Will retry.";
            if (TcpStatus == PortStatus.WasClosed) logger.LogTrace(message); else logger.LogWarning(message);
            TcpStatus = PortStatus.WasClosed;
          }

        SleepWithCancellation(2000); 
      }

      tcpServer.Stop();
      if (serialPort.IsOpen) serialPort.Close();
      logger.LogInformation("CatServer shutting down.");
    }

    public void Stop()
    {
      cts.Cancel();
    }
  }
}