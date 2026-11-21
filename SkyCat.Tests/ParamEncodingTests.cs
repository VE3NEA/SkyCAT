using System;
using System.Collections.Generic;
using System.Reflection;
using SkyCat;
using static SkyCat.CatCommandSet;

namespace SkyCat.Tests
{
  // covers the private encode/decode core (ParamToBytes / BytesToParam) that turns rigctld string
  // params into CAT bytes and back. reached by reflection so the production code stays untouched.
  public class ParamEncodingTests
  {
    private static readonly CatCommandSender Sender = new();
    private static readonly MethodInfo ParamToBytesMethod =
      typeof(CatCommandSender).GetMethod("ParamToBytes", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo BytesToParamMethod =
      typeof(CatCommandSender).GetMethod("BytesToParam", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static byte[] Encode(ParamInfo param, string value, int byteCount)
    {
      try
      {
        return (byte[])ParamToBytesMethod.Invoke(Sender, new object?[] { param, value, byteCount })!;
      }
      catch (TargetInvocationException ex)
      {
        throw ex.InnerException!;
      }
    }

    private static string? Decode(ParamInfo param, byte[] bytes)
    {
      try
      {
        return (string?)BytesToParamMethod.Invoke(Sender, new object?[] { param, bytes });
      }
      catch (TargetInvocationException ex)
      {
        throw ex.InnerException!;
      }
    }




    //----------------------------------------------------------------------------------------------
    //                                          BCD
    //----------------------------------------------------------------------------------------------
    [Fact]
    public void EncodesBcdBigEndian()
    {
      // 67.0 Hz CTCSS tone, sent by rigctld as tenths of Hz "670", into 3 bytes
      var param = new ParamInfo { Format = CatParamFormat.BCD_BE };
      Assert.Equal(new byte[] { 0x00, 0x06, 0x70 }, Encode(param, "670", 3));
    }

    [Fact]
    public void EncodesBcdLittleEndianAsReversedBigEndian()
    {
      var param = new ParamInfo { Format = CatParamFormat.BCD_LE };
      Assert.Equal(new byte[] { 0x70, 0x06, 0x00 }, Encode(param, "670", 3));
    }

    [Theory]
    [InlineData("670")]
    [InlineData("744")]
    [InlineData("1413")]
    public void BcdBigEndianRoundTrips(string value)
    {
      var param = new ParamInfo { Format = CatParamFormat.BCD_BE };
      var bytes = Encode(param, value, 3);
      Assert.Equal(value, Decode(param, bytes));
    }

    [Fact]
    public void BcdAppliesStepOnEncodeAndDecode()
    {
      // step divides on encode and multiplies on decode
      var param = new ParamInfo { Format = CatParamFormat.BCD_BE, Step = 10 };
      var bytes = Encode(param, "1000", 3);
      Assert.Equal(new byte[] { 0x00, 0x01, 0x00 }, bytes);
      Assert.Equal("1000", Decode(param, bytes));
    }

    [Fact]
    public void BcdThrowsWhenValueExceedsByteCount()
    {
      var param = new ParamInfo { Format = CatParamFormat.BCD_BE };
      Assert.Throws<ArgumentException>(() => Encode(param, "1000", 1));
    }

    [Fact]
    public void BcdThrowsOnNonNumericValue()
    {
      var param = new ParamInfo { Format = CatParamFormat.BCD_BE };
      Assert.Throws<ArgumentException>(() => Encode(param, "abc", 2));
    }




    //----------------------------------------------------------------------------------------------
    //                                          Enum
    //----------------------------------------------------------------------------------------------
    [Fact]
    public void EncodesEnumValue()
    {
      var param = new ParamInfo
      {
        Format = CatParamFormat.Enum,
        Values = new Dictionary<string, byte[]> { ["FM"] = new byte[] { 0x34 }, ["USB"] = new byte[] { 0x32 } }
      };
      Assert.Equal(new byte[] { 0x34 }, Encode(param, "FM", 1));
    }

    [Fact]
    public void EncodesEnumTranslatesUnderscoreToHyphen()
    {
      // mode enums cannot carry '-' so callers pass '_', which must map back to the '-' key
      var param = new ParamInfo
      {
        Format = CatParamFormat.Enum,
        Values = new Dictionary<string, byte[]> { ["CW-R"] = new byte[] { 0x37 } }
      };
      Assert.Equal(new byte[] { 0x37 }, Encode(param, "CW_R", 1));
    }

    [Fact]
    public void DecodesEnumValue()
    {
      var param = new ParamInfo
      {
        Format = CatParamFormat.Enum,
        Values = new Dictionary<string, byte[]> { ["FM"] = new byte[] { 0x34 }, ["USB"] = new byte[] { 0x32 } }
      };
      Assert.Equal("USB", Decode(param, new byte[] { 0x32 }));
    }

    [Fact]
    public void EncodeEnumThrowsOnUnknownKey()
    {
      var param = new ParamInfo
      {
        Format = CatParamFormat.Enum,
        Values = new Dictionary<string, byte[]> { ["FM"] = new byte[] { 0x34 } }
      };
      Assert.Throws<ArgumentException>(() => Encode(param, "AM", 1));
    }

    [Fact]
    public void DecodeEnumThrowsOnUnknownBytes()
    {
      var param = new ParamInfo
      {
        Format = CatParamFormat.Enum,
        Values = new Dictionary<string, byte[]> { ["FM"] = new byte[] { 0x34 } }
      };
      Assert.Throws<ArgumentException>(() => Decode(param, new byte[] { 0x99 }));
    }

    [Theory]
    [InlineData(0x30, "OFF")]
    [InlineData(0x31, "ON_CAT")]
    [InlineData(0x32, "ON_RADIO")]
    public void Ft991aReadPttDecodesAllThreeReplies(byte replyByte, string expected)
    {
      // the radio answers TX0; / TX1; / TX2; depending on whether PTT was keyed by CAT or by the radio itself
      var sender = new CatCommandSender();
      sender.SelectRadio("FT-991A");
      var groups = new CatCommandGroup?[]
        { sender.CommandSet.Duplex, sender.CommandSet.Split, sender.CommandSet.Simplex, sender.CommandSet.Transmitter, sender.CommandSet.Receiver };

      int checkedCount = 0;
      foreach (var group in groups)
      {
        if (group == null || !group.TryGetValue(CatCommand.read_ptt, out var info)) continue;
        var param = info.Messages[0].ReplyParam!;
        Assert.Equal(expected, Decode(param, new byte[] { replyByte }));
        checkedCount++;
      }
      Assert.True(checkedCount > 0);
    }




    //----------------------------------------------------------------------------------------------
    //                                          Text
    //----------------------------------------------------------------------------------------------
    [Fact]
    public void EncodesTextWithZeroPadding()
    {
      var param = new ParamInfo { Format = CatParamFormat.Text };
      // 8-digit value padded into 11 ASCII digits
      Assert.Equal(System.Text.Encoding.ASCII.GetBytes("00014074000"), Encode(param, "14074000", 11));
    }

    [Fact]
    public void TextRoundTrips()
    {
      var param = new ParamInfo { Format = CatParamFormat.Text };
      var bytes = Encode(param, "14074000", 11);
      Assert.Equal("14074000", Decode(param, bytes));
    }
  }
}
