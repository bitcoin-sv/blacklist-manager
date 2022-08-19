// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcGetPeerInfo
  {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("addr")]
    public string Address { get; set; }

    [JsonPropertyName("services")]
    public string Services { get; set; }

    [JsonPropertyName("relaytxes")]
    public bool RelayTxes { get; set; }

    [JsonPropertyName("lastsend")]
    public int LastSend { get; set; }

    [JsonPropertyName("lastrecv")]
    public int LastRecv { get; set; }

    [JsonPropertyName("sendsize")]
    public int SendSize { get; set; }

    [JsonPropertyName("recvsize")]
    public int RecvSize { get; set; }

    [JsonPropertyName("pausesend")]
    public bool PauseSend { get; set; }

    [JsonPropertyName("unpausesend")]
    public bool UnpauseSend { get; set; }

    [JsonPropertyName("bytessent")]
    public int BytesSent { get; set; }

    [JsonPropertyName("bytesrecv")]
    public int BytesRecv { get; set; }

    [JsonPropertyName("avgrecvbw")]
    public int AvgRecvbw { get; set; }

    [JsonPropertyName("associd")]
    public string AssocId { get; set; }

    [JsonPropertyName("streampolicy")]
    public string StreamPolicy { get; set; }

    //[JsonPropertyName("streams")]
    //public List<Stream> Streams { get; set; }

    [JsonPropertyName("conntime")]
    public int ConnTime { get; set; }

    [JsonPropertyName("timeoffset")]
    public int TimeOffset { get; set; }

    [JsonPropertyName("pingtime")]
    public double PingTime { get; set; }

    [JsonPropertyName("minping")]
    public double MinPing { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("subver")]
    public string Subver { get; set; }

    [JsonPropertyName("inbound")]
    public bool Inbound { get; set; }

    [JsonPropertyName("addnode")]
    public bool AddNode { get; set; }

    [JsonPropertyName("startingheight")]
    public int StartingHeight { get; set; }

    [JsonPropertyName("txninvsize")]
    public int TxnInvSize { get; set; }

    [JsonPropertyName("banscore")]
    public int BanScore { get; set; }

    [JsonPropertyName("synced_headers")]
    public int SyncedHeaders { get; set; }

    [JsonPropertyName("synced_blocks")]
    public int SyncedBlocks { get; set; }

    [JsonPropertyName("inflight")]
    public List<object> Inflight { get; set; }

    [JsonPropertyName("whitelisted")]
    public bool Whitelisted { get; set; }

    //[JsonPropertyName("bytessent_per_msg")]
    //public BytessentPerMsg BytessentPerMsg { get; set; }

    //[JsonPropertyName("bytesrecv_per_msg")]
    //public BytesrecvPerMsg BytesrecvPerMsg { get; set; }
  }
}
