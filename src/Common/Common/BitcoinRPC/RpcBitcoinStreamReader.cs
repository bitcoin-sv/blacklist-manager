// Copyright (c) 2021 Bitcoin Association

using Common.BitcoinRpc;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Common.BitcoinRPC
{
  public class RPCBitcoinStreamReader : Stream
  {
    readonly StreamReader streamReader;

    readonly CancellationToken? token;

    public int TotalBytesRead { get; private set; }


    public RPCBitcoinStreamReader(StreamReader streamReader, CancellationToken? token) : base()
    {
      this.streamReader = streamReader;
      this.token = token;
    }
    public override bool CanRead => streamReader.BaseStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => streamReader.BaseStream.Length / 2;

    /// <summary>
    /// Position values are correct only if they target position inside the value part of the "result" field 
    /// </summary>
    public override long Position { get => streamReader.BaseStream.Position / 2; set => streamReader.BaseStream.Position = value * 2; }

    public override void Flush()
    {
      throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      token?.ThrowIfCancellationRequested();

      // Because the data is HEX encoded, we need to read 2 chars for each returned byte
      var charBuffer = new char[count * 2];
      var hexChar = new char[2];
      var readCount = streamReader.ReadBlock(charBuffer, offset, count * 2);

      if (readCount == 0)
      {
        return 0;
      }

      // The amount of data read from stream should always be twice the size of count parameter
      if (readCount != (count * 2))
      {
        throw new RpcException("Error when executing bitcoin RPC method. RPC response contains invalid HEX data in JSON response", null);
      }

      for (int i = 0; i < readCount; i += 2)
      {
        hexChar[0] = charBuffer[i];
        hexChar[1] = charBuffer[i + 1];
        buffer[i / 2] = (byte)int.Parse(hexChar, NumberStyles.AllowHexSpecifier);
        TotalBytesRead++;
      }
      return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotImplementedException();
    }
  }
}
