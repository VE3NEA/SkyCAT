using System;
using System.Collections.Generic;
using System.IO;
using SkyCat;

namespace SkyCat.Tests
{
  // exercises CatCommandSet.FromJson: real-file loading plus the Validate() rule set
  public class CatCommandSetTests
  {
    // wraps a single command-info body into a minimal, otherwise-valid command set under "simplex"
    private static string CommandSet(string commandInfoJson, string commandName = "write_rx_mode") =>
      $$"""{ "id": 1, "simplex": { "{{commandName}}": {{commandInfoJson}} } }""";

    public static IEnumerable<object[]> RigFiles()
    {
      var dir = Path.Combine(AppContext.BaseDirectory, "Rigs");
      foreach (var file in Directory.GetFiles(dir, "*.json"))
        yield return new object[] { Path.GetFileName(file) };
    }




    //----------------------------------------------------------------------------------------------
    //                                     real rig files
    //----------------------------------------------------------------------------------------------
    [Theory]
    [MemberData(nameof(RigFiles))]
    public void EveryShippedRigFileParsesAndValidates(string fileName)
    {
      var path = Path.Combine(AppContext.BaseDirectory, "Rigs", fileName);
      var json = File.ReadAllText(path);

      var commandSet = CatCommandSet.FromJson(json);

      Assert.NotNull(commandSet);
      // a rig must define at least one operating-mode group
      Assert.True(commandSet.Duplex != null || commandSet.Split != null || commandSet.Simplex != null
        || commandSet.Transmitter != null || commandSet.Receiver != null);
    }

    [Fact]
    public void Ic9700TeardownTurnsTransceiveBackOn()
    {
      var path = Path.Combine(AppContext.BaseDirectory, "Rigs", "IC-9700.json");
      var commandSet = CatCommandSet.FromJson(File.ReadAllText(path));
      byte?[] transceiveOn = [0xFE, 0xFE, 0xA2, 0xE0, 0x1A, 0x05, 0x01, 0x27, 0x01, 0xFD];

      foreach (var group in new[] { commandSet.Duplex, commandSet.Split, commandSet.Simplex })
        Assert.Equal(transceiveOn, group![CatCommand.teardown].Messages[0].Command);
    }

    [Fact]
    public void RigFolderIsNotEmpty()
    {
      var dir = Path.Combine(AppContext.BaseDirectory, "Rigs");
      Assert.NotEmpty(Directory.GetFiles(dir, "*.json"));
    }




    //----------------------------------------------------------------------------------------------
    //                                    valid deserialization
    //----------------------------------------------------------------------------------------------
    [Fact]
    public void ParsesFieldsFromMinimalCommandSet()
    {
      var json = """
        {
          "id": 1234,
          "echo": true,
          "bad_reply": ["FE", "FD"],
          "simplex": {
            "write_ptt_on": { "messages": [ { "command": ["54", "58", "3B"] } ] }
          }
        }
        """;

      var commandSet = CatCommandSet.FromJson(json);

      Assert.Equal(1234, commandSet.Id);
      Assert.True(commandSet.Echo);
      Assert.Equal(new byte?[] { 0xFE, 0xFD }, commandSet.BadReply);
      Assert.NotNull(commandSet.Simplex);
      Assert.True(commandSet.Simplex.ContainsKey(CatCommand.write_ptt_on));
      Assert.Null(commandSet.Duplex);
    }

    [Fact]
    public void ParsesCommandWithParamAndEnumReply()
    {
      var json = CommandSet("""
        {
          "messages": [
            {
              "command": ["4D", "44", null, "3B"],
              "reply": ["4D", "44", null, "3B"],
              "command_param": { "format": "enum", "values": { "FM": ["34"], "USB": ["32"] } },
              "reply_param":   { "format": "enum", "values": { "FM": ["34"], "USB": ["32"] } }
            }
          ]
        }
        """);

      var commandSet = CatCommandSet.FromJson(json);

      var info = commandSet.Simplex[CatCommand.write_rx_mode];
      Assert.Single(info.Messages);
      Assert.Equal(CatParamFormat.Enum, info.Messages[0].CommandParam!.Format);
    }




    //----------------------------------------------------------------------------------------------
    //                                   validation failures
    //----------------------------------------------------------------------------------------------
    [Fact]
    public void RejectsCommandSetWithNoOperatingModes()
    {
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson("""{ "id": 1 }"""));
      Assert.Contains("RadioType", ex.Message);
    }

    [Fact]
    public void RejectsEmptyBadReply()
    {
      var json = """{ "id": 1, "bad_reply": [], "simplex": { "write_ptt_on": { "messages": [ { "command": ["54"] } ] } } }""";
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("BadReply", ex.Message);
    }

    [Fact]
    public void RejectsEmptyCommandGroup()
    {
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson("""{ "id": 1, "simplex": {} }"""));
      Assert.Contains("at least one command", ex.Message);
    }

    [Fact]
    public void RejectsBlankCommandBytes()
    {
      var json = CommandSet("""{ "messages": [ { "command": [] } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("cannot be blank", ex.Message);
    }

    [Fact]
    public void RejectsNullsWithoutCommandParam()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["46", null, "3B"] } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("CommandParam is not defined", ex.Message);
    }

    [Fact]
    public void RejectsCommandParamWithoutNulls()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["46", "3B"], "command_param": { "format": "Text" } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("does not contain nulls", ex.Message);
    }

    [Fact]
    public void RejectsNonContiguousNulls()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["46", null, "20", null, "3B"], "command_param": { "format": "Text" } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("one block", ex.Message);
    }

    [Fact]
    public void RejectsParamStartMismatch()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["46", null, null, "3B"], "command_param": { "format": "Text", "start": 0 } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("start does not match", ex.Message);
    }

    [Fact]
    public void RejectsParamLengthMismatch()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["46", null, null, "3B"], "command_param": { "format": "Text", "length": 5 } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("length does not match", ex.Message);
    }

    [Fact]
    public void RejectsEnumParamWithoutValues()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", null, "3B"], "command_param": { "format": "Enum" } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("values are not defined", ex.Message);
    }

    [Fact]
    public void RejectsEnumValueWithWrongLength()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", null, "3B"], "command_param": { "format": "Enum", "values": { "FM": ["34", "35"] } } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("length matching", ex.Message);
    }

    [Fact]
    public void RejectsValuesOnNonEnumFormat()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", null, "3B"], "command_param": { "format": "BCD_BE", "values": { "FM": ["34"] } } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("format is not Enum", ex.Message);
    }

    [Fact]
    public void RejectsMaskOnCommandParam()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", null, "3B"], "command_param": { "format": "BCD_BE", "mask": "0F" } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("mask not allowed", ex.Message);
    }

    [Fact]
    public void RejectsReplyParamWithoutReply()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", "3B"], "reply_param": { "format": "Text" } } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("reply is not present", ex.Message);
    }

    [Fact]
    public void RejectsIgnoreErrorWithReplyParam()
    {
      var json = CommandSet("""{ "messages": [ { "command": ["4D", "3B"], "reply": ["4D", null, "3B"], "reply_param": { "format": "Text" }, "ignore_error": true } ] }""");
      var ex = Assert.Throws<FormatException>(() => CatCommandSet.FromJson(json));
      Assert.Contains("ignore_error is not allowed", ex.Message);
    }
  }
}
