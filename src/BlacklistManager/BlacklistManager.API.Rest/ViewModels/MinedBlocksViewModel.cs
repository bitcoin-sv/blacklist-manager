// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.API.Rest.ViewModels
{
  public class MinedBlocksViewModel
  {
    public string PublicKey { get; set; }
    public string PublicKeyAddress { get; set; }
    public int NumberOfMinedBlocks { get; set; }
    public int NumberOfBlocksToCheck { get; set; }
  }
}
