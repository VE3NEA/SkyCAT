using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SkyCat
{
  public enum OperatingMode
  {
    Duplex,
    Split,
    Simplex,
    Transmitter,
    Receiver,
  }

  public enum CatCommand
  {
    setup,
    read_rx_frequency,
    read_tx_frequency,
    read_rx_mode,
    read_tx_mode,
    read_ptt,
    write_rx_frequency,
    write_tx_frequency,
    write_rx_mode,
    write_tx_mode,
    write_ptt_off,
    write_ptt_on,
  }

  public enum CatRestriction
  {
    none,
    when_receiving,
    when_transmitting,
    when_setting_up,
  }

  public enum CatParamFormat
  {
    Text,
    Enum,
    BCD_LE,
    BCD_BE,
  }

  public class CatCommandSet
  {
    public static CatCommandSet FromJson(string json)
    {
      var settings = new JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
        Converters =
        {
          new HexStringToNullableByteArrayConverter(),
          new DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>(),
          new StringEnumConverter()
        }
      };
      var commandSet = JsonConvert.DeserializeObject<CatCommandSet>(json, settings)!;
      commandSet.Validate();
      return commandSet;
    }




    //--------------------------------------------------------------------------------------------------------------------
    //                                                   data
    //--------------------------------------------------------------------------------------------------------------------
    public int Id { get; set; }
    public bool Echo { get; set; }

    // currently not used
    [JsonProperty("default_baud_rate")]
    public int DefaultBaudRate { get; set; } = 9600;

    // currently not used
    [JsonProperty("cross_band_split")]
    public bool CrossBandSplit { get; set; }


    [JsonProperty("bad_reply")]
    [JsonConverter(typeof(HexStringToNullableByteArrayConverter))]
    public byte?[]? BadReply { get; set; }

    [JsonConverter(typeof(DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>))]
    public CatCommandGroup? Duplex { get; set; }

    [JsonConverter(typeof(DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>))]
    public CatCommandGroup? Split { get; set; }

    [JsonConverter(typeof(DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>))]
    public CatCommandGroup Simplex { get; set; }

    [JsonConverter(typeof(DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>))]
    public CatCommandGroup Transmitter { get; set; }

    [JsonConverter(typeof(DictionaryEnumToObjectConverter<CatCommandGroup, CatCommand, CatCommandInfo>))]
    public CatCommandGroup Receiver { get; set; }


    // a command, such as Setup, may require more than one message to be sent to the radio, hence []
    public class CatCommandGroup : Dictionary<CatCommand, CatCommandInfo>;

    public class CatCommandInfo
    {
      public CatMessage[] Messages { get; set; }

      [JsonProperty("alt_messages")]
      public CatMessage[]? AltMessages { get; set; }

      [JsonConverter(typeof(StringEnumConverter))]
      public CatRestriction Restriction { get; set; }
    }


    public class CatMessage
    {
      public string? Comment { get; set; }

      [JsonConverter(typeof(HexStringToNullableByteArrayConverter))]
      public byte?[] Command { get; set; }

      [JsonConverter(typeof(HexStringToNullableByteArrayConverter))]
      public byte?[]? Reply { get; set; }

      [JsonProperty("command_param")]
      public ParamInfo? CommandParam { get; set; }

      [JsonProperty("reply_param")]
      public ParamInfo? ReplyParam { get; set; }

      [JsonProperty("ignore_error")]
      public bool IgnoreError { get; set; }
    }

    public class ParamInfo
    {
      [JsonConverter(typeof(StringEnumConverter))]
      public CatParamFormat Format { get; set; }

      public double? Step { get; set; }

      public int? Start { get; set; }

      public int? Length { get; set; }

      [JsonConverter(typeof(HexStringToByteArrayConverter))]
      public byte[]? Mask { get; set; }

      [JsonConverter(typeof(DictionaryStringToByteArrayConverter))]
      public Dictionary<string, byte[]>? Values { get; set; }
    }




    //--------------------------------------------------------------------------------------------------------------------
    //                                                 validation
    //--------------------------------------------------------------------------------------------------------------------
    private void Validate()
    {
      if (Duplex == null && Split == null && Simplex == null && Transmitter == null && Receiver == null)
        throw new FormatException("At least one RadioType must be defined.");

      if (BadReply != null && BadReply.Length == 0)
        throw new FormatException("BadReply must be null or contain at least one byte.");

      var operatingModes = (OperatingMode[])Enum.GetValues(typeof(OperatingMode));
      foreach (OperatingMode operatingMode in operatingModes)
        ValidateCommandGroup(operatingMode);
    }

    private void ValidateCommandGroup(OperatingMode operatingMode)
    {
      CatCommandGroup? group = (CatCommandGroup?)GetType().GetProperty($"{operatingMode}")!.GetValue(this, null);
      if (group == null) return;

      if (group.Count == 0) throw new FormatException($"Command group {operatingMode} must contain at least one command.");

      foreach (var kv in group) ValicateCommandInfo(operatingMode, kv.Key, kv.Value);
    }

    private void ValicateCommandInfo(OperatingMode operatingMode, CatCommand command, CatCommandInfo commandInfo)
    {
      if (commandInfo == null) return;

      // messages
      if (commandInfo.Messages == null || commandInfo.Messages.Length == 0)
        throw new FormatException($"{operatingMode}.{command} must contain at least one message in Messages.");

      for (int i = 0; i < commandInfo.Messages.Length; i++)
        ValicateCommandMessage(operatingMode, command, commandInfo.Messages[i], i);

      // alt messages
      if (commandInfo.AltMessages != null && commandInfo.AltMessages.Length == 0)
        throw new FormatException($"{operatingMode}.{command}.AltMessages must be null or contain at least one message.");

      if (commandInfo.AltMessages != null)
        for (int i = 0; i < commandInfo.AltMessages.Length; i++)
          ValicateCommandMessage(operatingMode, command, commandInfo.AltMessages[i], i);
    }

    private void ValicateCommandMessage(OperatingMode operatingMode, CatCommand command, CatMessage message, int i)
    {
      string infoName = $"{operatingMode}.{command}[{i}]";


      // COMMAND

      // command is required
      if (message.Command == null || message.Command.Length == 0)
        throw new FormatException($"{infoName}.command cannot be blank.");

      int commandNullCount = message.Command.Count(b => b == null);
      int replyNullCount = message.Reply?.Count(b => b == null) ?? 0;

      // command params and nulls in the command must either both be present or both not present
      if (commandNullCount == 0 && message.CommandParam != null)
        throw new FormatException($"{infoName}.command does not contain nulls but CommandParam is defined.");
      if (commandNullCount > 0 && message.CommandParam == null)
        throw new FormatException($"{infoName}.command does contains nulls but CommandParam is not defined.");

      if (message.CommandParam != null)
      {
        // nulls in command must be in one block
        int commandNullPos = Array.IndexOf(message.Command, null);
        if (message.Command.Skip(commandNullPos).Take(commandNullCount).Any(b => b != null))
          throw new FormatException($"{infoName}.command must have all nulls in one block.");

        // if Start and/or Length are present, they must match the nulls in command
        if (message.CommandParam.Start.HasValue && message.CommandParam.Start.Value != commandNullPos)
          throw new FormatException($"{infoName}.command_param.start does not match the position of nulls in command.");
        if (message.CommandParam.Length.HasValue && message.CommandParam.Length.Value != commandNullCount)
          throw new FormatException($"{infoName}.command_param.length does not match the number of nulls in command.");
      }

      if (message.ReplyParam != null && message.IgnoreError)
        throw new FormatException($"{infoName}.ignore_error is not allowed if ReplyParam is defined.");

      // REPLY

      if (message.Reply == null && message.ReplyParam != null)
        throw new FormatException($"{infoName}.reply is not present but ReplyParam is defined.");

      if (message.Reply != null)
      {
        // if Reply is defined, it must not be empty
        if (message.Reply.Length == 0)
          throw new FormatException($"{infoName}.reply cannot be empty if defined.");

        // ReplyParam requires nulls in Reply
        if (message.ReplyParam != null && replyNullCount == 0)
          throw new FormatException($"{infoName}.reply does not contain nulls but ReplyParam is defined.");
      }


      // COMMAND PARAM

      if (message.CommandParam != null)
      {
        if (message.CommandParam.Format == CatParamFormat.Enum)
        {
          // values are required for Enum format
          if (message.CommandParam.Values == null || message.CommandParam.Values.Count == 0)
            throw new FormatException($"{infoName}.command_param.format is Enum but values are not defined or empty.");

          // values must have proper lengths
          if (message.CommandParam.Values.Any(v => v.Value == null || v.Value.Length != commandNullCount))
            throw new FormatException($"{infoName}.command_param.values must each have a length matching the nulls in the command.");
        }
        // values are not allowed for formats other than Enum
        else if (message.CommandParam.Values != null)
          throw new FormatException($"{infoName}.command_param.values is defined but format is not Enum.");

        // mask not allowed for command
        if (message.CommandParam.Mask != null)
          throw new FormatException($"{infoName}.command_param.mask not allowed for command parameters.");
      }

      // REPLY PARAM

      if (message.ReplyParam != null)
      {
        int start = message.ReplyParam.Start ?? Array.IndexOf(message.Reply!, null);
        int length = message.ReplyParam.Length ?? message.Reply!.Skip(start).Count(b => b == null);

        if (message.ReplyParam.Format == CatParamFormat.Enum)
        {
          // values are required for Enum format
          if (message.ReplyParam.Values == null || message.ReplyParam.Values.Count == 0)
            throw new FormatException($"{infoName}.reply_param.format is Enum but values are not defined or empty.");

          // values must have proper lengths
          if (message.ReplyParam.Values.Any(v => v.Value == null || v.Value.Length != length))
            throw new FormatException($"{infoName}.reply_param.values must each have a length matching the nulls in the reply.");
        }
        // values are not allowed for formats other than Enum
        else if (message.ReplyParam.Values != null)
          throw new FormatException($"{infoName}.reply_param.values is defined but format is not Enum.");

        // mask must have correct length
        if (message.ReplyParam.Mask != null && message.ReplyParam.Mask.Length != length)
          throw new FormatException($"{infoName}.reply_param.mask must match the number parameter length.");

        // param in reply must be all nulls
        if (message.Reply!.Skip(start).Take(length).Any(b => b != null))
          throw new FormatException($"{infoName}.reply_param start and length do not match the nulls in reply.");
      }
    }
  }
}