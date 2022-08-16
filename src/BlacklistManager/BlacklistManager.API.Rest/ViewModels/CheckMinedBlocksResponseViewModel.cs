// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class CheckMinedBlocksResponseViewModel
  {
    public List<MinedBlocksViewModel> MinedBlocks { get; set; }
  }
}
