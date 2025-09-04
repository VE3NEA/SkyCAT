using Newtonsoft.Json;

namespace SkyCat
{
  public class RadioCapabilities
  {
    public string model { get; set; }
    public bool cross_band_split { get; set; }

    public AvailableCommands? simplex { get; set; }
    public AvailableCommands? split { get; set; }
    public AvailableCommands? duplex { get; set; }

    public RadioCapabilities(string model, bool cross_band_split,
      AvailableCommands? simplex, AvailableCommands? split, AvailableCommands? duplex)
    {
      this.model = model;
      this.cross_band_split = cross_band_split;
      this.simplex = simplex;
      this.split = split;
      this.duplex = duplex;
    }

    public string ToJson()
    {
      return JsonConvert.SerializeObject(this, Formatting.None);
    }

    public static RadioCapabilities FromCatCommandSet(string name, CatCommandSet commandSet)
    {
      var capabilities = new RadioCapabilities(
          name,
          commandSet.CrossBandSplit,
          ExtractAvailableCommands(commandSet.Simplex),
          ExtractAvailableCommands(commandSet.Split),
          ExtractAvailableCommands(commandSet.Duplex)
      );

      return capabilities;
    }

    private static AvailableCommands? ExtractAvailableCommands(CatCommandSet.CatCommandGroup? commandGroup)
    {
      if (commandGroup == null) return null;

      var receivingCommands = new List<string>();
      var transmittingCommands = new List<string>();
      var setupCommands = new List<string>();

      foreach (var cmd in commandGroup)
      {
        if (cmd.Value == null) continue;
        string commandName = cmd.Key.ToString();
        var restriction = cmd.Value.Restriction;

        switch (restriction)
        {
          case CatRestriction.none:
            receivingCommands.Add(commandName);
            transmittingCommands.Add(commandName);
            setupCommands.Add(commandName);
            break;

          case CatRestriction.when_receiving:
            receivingCommands.Add(commandName);
            setupCommands.Add(commandName);
            break;

          case CatRestriction.when_transmitting:
            transmittingCommands.Add(commandName);
            break;

          case CatRestriction.when_setting_up:
            setupCommands.Add(commandName);
            break;
        }
      }

      return new AvailableCommands
      {
        when_receiving = receivingCommands.ToArray(),
        when_transmitting = transmittingCommands.ToArray(),
        when_setting_up = setupCommands.ToArray()
      };
    }
  }


  public class AvailableCommands
  {
    public string[] when_receiving { get; set; }
    public string[] when_transmitting { get; set; }
    public string[] when_setting_up { get; set; }
  }
}
