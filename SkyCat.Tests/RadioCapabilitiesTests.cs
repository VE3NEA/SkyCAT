using SkyCat;

namespace SkyCat.Tests
{
  // verifies how command restrictions map into the capabilities advertised over the "a" command
  public class RadioCapabilitiesTests
  {
    private const string Json = """
      {
        "id": 1,
        "cross_band_split": true,
        "simplex": {
          "write_ptt_on": { "messages": [ { "command": ["54"] } ] },
          "read_rx_mode": { "messages": [ { "command": ["4D"] } ], "restriction": "when_receiving" },
          "read_tx_mode": { "messages": [ { "command": ["4D"] } ], "restriction": "when_transmitting" },
          "setup":        { "messages": [ { "command": ["41"] } ], "restriction": "when_setting_up" },
          "teardown":     { "messages": [ { "command": ["42"] } ], "restriction": "when_setting_up" }
        }
      }
      """;

    private static RadioCapabilities Build()
    {
      var commandSet = CatCommandSet.FromJson(Json);
      return RadioCapabilities.FromCatCommandSet("TestRig", commandSet);
    }


    [Fact]
    public void CarriesModelAndCrossBandSplit()
    {
      var caps = Build();
      Assert.Equal("TestRig", caps.model);
      Assert.True(caps.cross_band_split);
    }

    [Fact]
    public void UndefinedGroupsBecomeNull()
    {
      var caps = Build();
      Assert.NotNull(caps.simplex);
      Assert.Null(caps.split);
      Assert.Null(caps.duplex);
    }

    [Fact]
    public void RestrictionNoneAppearsInAllThreeLists()
    {
      var s = Build().simplex!;
      Assert.Contains("write_ptt_on", s.when_receiving);
      Assert.Contains("write_ptt_on", s.when_transmitting);
      Assert.Contains("write_ptt_on", s.when_setting_up);
    }

    [Fact]
    public void WhenReceivingAppearsInReceivingAndSetupOnly()
    {
      var s = Build().simplex!;
      Assert.Contains("read_rx_mode", s.when_receiving);
      Assert.Contains("read_rx_mode", s.when_setting_up);
      Assert.DoesNotContain("read_rx_mode", s.when_transmitting);
    }

    [Fact]
    public void WhenTransmittingAppearsInTransmittingOnly()
    {
      var s = Build().simplex!;
      Assert.Contains("read_tx_mode", s.when_transmitting);
      Assert.DoesNotContain("read_tx_mode", s.when_receiving);
      Assert.DoesNotContain("read_tx_mode", s.when_setting_up);
    }

    [Fact]
    public void WhenSettingUpAppearsInSetupOnly()
    {
      var s = Build().simplex!;
      Assert.Contains("setup", s.when_setting_up);
      Assert.DoesNotContain("setup", s.when_receiving);
      Assert.DoesNotContain("setup", s.when_transmitting);
    }

    [Fact]
    public void TeardownIsNotAdvertised()
    {
      // skycatd sends teardown itself, so clients should not see it
      var s = Build().simplex!;
      Assert.DoesNotContain("teardown", s.when_setting_up);
      Assert.DoesNotContain("teardown", s.when_receiving);
      Assert.DoesNotContain("teardown", s.when_transmitting);
    }

    [Fact]
    public void ToJsonEmitsModelAndGroups()
    {
      var json = Build().ToJson();
      Assert.Contains("\"model\":\"TestRig\"", json);
      Assert.Contains("when_transmitting", json);
    }
  }
}
