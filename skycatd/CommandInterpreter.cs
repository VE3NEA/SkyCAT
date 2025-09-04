using Microsoft.Extensions.Logging;
using Serilog.Core;
using SkyCat;
namespace skycatd
{
  public class CommandInterpreter
  {
    public readonly CatCommandSender CommandSender;

    public CommandInterpreter(Options options, ILogger? logger)
    {
      CommandSender = new CatCommandSender(logger);
      CommandSender.SelectRadio(options.Model);
      CommandSender.SerialPort.PortName = options.RigFile;
      CommandSender.SerialPort.BaudRate = options.SerialSpeed ?? CommandSender.CommandSet.DefaultBaudRate;
    }

    public string Execute(string command)
    {
      var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (args.Length == 0) return "RPRT -1";

      return args[0] switch
      {
        "a" => CommandSender.ListCapabilities(),
        "f" => CmdF(),
        "i" => CmdI(),
        "m" => CmdM(),
        "x" => CmdX(),
        "t" => CmdT(),
        "F" when args.Length == 2 && int.TryParse(args[1], out var fInt) => CmdFInt(fInt),
        "I" when args.Length == 2 && int.TryParse(args[1], out var iInt) => CmdIInt(iInt),
        "M" when args.Length == 3 && int.TryParse(args[2], out var mZero) => CmdMStr0(args[1], mZero),
        "X" when args.Length == 3 && int.TryParse(args[2], out var xZero) => CmdXStr0(args[1], xZero),
        "T" when args.Length == 2 && (args[1] == "0") => CmdT0("OFF"),
        "T" when args.Length == 2 && (args[1] == "1") => CmdT1("ON"),

        // setup
        "S" when args.Length == 3 && args[1] == "0" => Setup(OperatingMode.Simplex),
        "S" when args.Length == 3 && args[1] == "1" && args[2] != "Sub" => Setup(OperatingMode.Split),
        "S" when args.Length == 3 && args[1] == "1" && args[2] == "Sub" => Setup(OperatingMode.Duplex),
        "U" when args.Length == 3 && args[1] == "SATMODE" && args[2] == "1" => Setup(OperatingMode.Duplex),
        "U" when args.Length == 2 && Enum.TryParse<OperatingMode>(args[1], out var mode) => Setup(mode),

        // ignore
        "U" when args.Length == 3 && args[1] == "SATMODE" && args[2] != "1" => Ignore(),
        "U" when args.Length == 3 && args[1] == "DUAL_WATCH" => Ignore(),
        "V" => Ignore(),

        _ => "RPRT -11"
      };
    }

    private string Ignore()
    {
      CommandSender.Log?.LogTrace("  Command ignored");
      return "RPRT 0";
    }

    private string CmdF() => SendCommandIfAvailable(CatCommand.read_rx_frequency);
    private string CmdI() => SendCommandIfAvailable(CatCommand.read_tx_frequency);
    private string CmdM() => SendCommandIfAvailable(CatCommand.read_rx_mode);
    private string CmdX() => SendCommandIfAvailable(CatCommand.read_tx_mode);
    private string CmdT() => SendCommandIfAvailable(CatCommand.read_ptt);
    private string CmdFInt(int value) => SendCommandIfAvailable(CatCommand.write_rx_frequency, value.ToString());
    private string CmdIInt(int value) => SendCommandIfAvailable(CatCommand.write_tx_frequency, value.ToString());
    private string CmdMStr0(string str, int zero) => SendCommandIfAvailable(CatCommand.write_rx_mode, str);
    private string CmdXStr0(string str, int zero) => SendCommandIfAvailable(CatCommand.write_tx_mode, str);
    private string CmdT0(string value) => SendCommandIfAvailable(CatCommand.write_ptt_off, value);
    private string CmdT1(string value) => SendCommandIfAvailable(CatCommand.write_ptt_on, value);

    private string Setup(OperatingMode mode)
    {
      try
      {
        CommandSender.SetupRadio(mode);
        return "RPRT 0";
      }
      catch (InvalidReplyException ex)
      {
        CommandSender.Log?.LogError($"Setup command failed: {ex.Message}");
        return "RPRT -1";
      }
      catch (Exception ex)
      {
        CommandSender.Log?.LogError(ex, $"Setup command failed: {ex.Message}");
        return "RPRT -1";
      }
    }


    private string SendCommandIfAvailable(CatCommand command, string? paramValue = null)
    {
      if (command != CatCommand.setup && !CommandSender.IsCommandAvailable(command)) return "RPRT -11";

      try
      {
        return CommandSender.SendCommand(command, paramValue) ?? "RPRT 0";
      }

      catch (ArgumentException ex)
      {
        CommandSender.Log?.LogError($"Command failed: {ex.Message}");
        return "RPRT -1";
      }
      catch (TimeoutException ex)
      {
        CommandSender.Log?.LogError($"Command failed: {ex.Message}");
        return "RPRT -5";
      }
      catch (InvalidOperationException ex)
      {
        CommandSender.Log?.LogError($"Command failed: {ex.Message}");
        return "RPRT -6";
      }
      catch (InvalidReplyException ex)
      {
        CommandSender.Log?.LogError($"Command failed: {ex.Message}");
        return "RPRT -9";
      }
      catch (Exception ex)
      {
        CommandSender.Log?.LogError(ex, "Command failed.");
        return "RPRT -7";
      }
    }
  }
}