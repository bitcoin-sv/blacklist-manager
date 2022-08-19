// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Caching.Memory;
using System;
using Microsoft.Extensions.Logging;
using AlertManager.Web;
using System.Runtime.InteropServices;
using Common.Bitcoin;

namespace Common
{
  public static class CacheClass
  {
    public static void InitCaches(long memCacheSize, int absoluteExpirationMin, int slidingExpirationMin)
    {
      var memCacheOptions = new MemoryCacheOptions();
      memCacheOptions.SizeLimit = memCacheSize;

      TxCache = new NotaryCache<string, string>(memCacheOptions, absoluteExpirationMin, slidingExpirationMin);
      BlockHeaderCache = new NotaryCache<long, BlockHeaderItem>(memCacheOptions, absoluteExpirationMin, slidingExpirationMin);
    }

    internal static NotaryCache<string, string> TxCache { get; private set; }
    internal static NotaryCache<long, BlockHeaderItem> BlockHeaderCache { get; private set; }
  }

  internal class NotaryCache<N, T> : MemoryCache
  {
    private readonly int AbsoluteExpirationMin;
    private readonly int SlidingExpirationMin;

    private TimeSpan AbsoluteExpiration => new TimeSpan(AbsoluteExpirationMin / 60, AbsoluteExpirationMin % 60, 0);

    private TimeSpan SlidingExpiration => new TimeSpan(SlidingExpirationMin / 60, SlidingExpirationMin % 60, 0);

    public NotaryCache(MemoryCacheOptions memoryCacheOptions, int absoluteExpirationMin, int slidingExpirationMin) : base(memoryCacheOptions) 
    {
      AbsoluteExpirationMin = absoluteExpirationMin;
      SlidingExpirationMin = slidingExpirationMin;
    }

    public bool TryAdd(N key, T value)
    {
      if (key == null)
        return false;
      if (value == null)
        return false;
      var memCacheEntryOption = new MemoryCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = AbsoluteExpiration,
        SlidingExpiration = SlidingExpiration,
        Size = GetObjectSize(value),
      };

      this.Set(key, value, memCacheEntryOption);
      return true;
    }

    private long GetObjectSize(T value)
    {
      long objectSize;
      if (value is string)
      {
        objectSize = ((string)(object)value).Length * 2;
      }
      else if (value is BlockHeaderItem)
      {
        objectSize = ((BlockHeaderItem)(object)value).GetObjSize();
      }
      else
      {
        objectSize = Marshal.SizeOf(value);
      }
      return objectSize;
    }
  }
}
