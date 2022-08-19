// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient
{
  public class RpcRequest
  {
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public IList<object> Parameters { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    public RpcRequest(int id, string method, params object[] parameters)
    {
      Id = id;
      Method = method;

      if (parameters != null)
      {
        Parameters = parameters.ToList<object>();
      }
      else
      {
        Parameters = new List<object>();
      }
    }

    public string GetJSON()
    {
      return JsonSerializer.Serialize(this, new JsonSerializerOptions() {  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    public byte[] GetBytes()
    {      
      return Encoding.UTF8.GetBytes(GetJSON());
    }
  }
}
