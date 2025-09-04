using CommandLine;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using System.ComponentModel.DataAnnotations;

namespace skycatd
{
  public class Options
  {
    [Option('m', "model", Required = false, HelpText = "Radio model number")]
    [Range(1, int.MaxValue, ErrorMessage = "Model number must be greater than 0")]
    public string Model { get; set; }

    [Option('r', "rig-file", Required = false, HelpText = "Serial port name")]
    [Required(AllowEmptyStrings = true, ErrorMessage = "Serial port name is required")]
    public string RigFile { get; set; } = string.Empty;

    [Option('s', "serial-speed", Required = false, HelpText = "Serial port Baud rate.")]
    public int? SerialSpeed { get; set; }

    [Option('t', "port", Required = false, HelpText = "TCP listening port.", Default = 4532)]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; }

    [Option('l', "list", Required = false, HelpText = "List available model numbers and exit.")]
    public bool List { get; set; }

    [Option('a', "all", Required = false, HelpText = "Print capabilities of all radios and exit.")]
    public bool All { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose logging using multiple v's, -vvvvv.")]
    public string? Verbose { get; set; }

    [Option('f', "file-log", Required = false, Default = false, HelpText = "Save the log to a file.")]
    public bool FileLog { get; set; }

    // -vv and more = verbose, -v or no option = warning
    public LogEventLevel LogLevel => Verbose != null ? LogEventLevel.Verbose : LogEventLevel.Warning;

    // Returns true if neither -l nor -a is specified, in which case -m and -r are required
    public bool RequiresModelAndPort => !(List || All);

    // Add this method after the RequiresModelAndPort property
    public bool Validate()
    {
      if (!RequiresModelAndPort)
        return true;  // No validation needed, so it's valid
      
      var errors = new List<string>();
      
      if (string.IsNullOrEmpty(Model))
        errors.Add("Model number must be greater than 0.");
        
      if (string.IsNullOrWhiteSpace(RigFile))
        errors.Add("Serial port name is required.");
        
      if (errors.Any())
      {
        foreach (var error in errors)
          Console.Error.WriteLine(error);
        return false;  // Return false to indicate validation failed
      }
      
      return true;  // Validation passed
    }
  }
}
