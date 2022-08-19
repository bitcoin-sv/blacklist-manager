// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class TxOutController : ControllerBase
  {
    private readonly ICourtOrderRepository _courtOrderRepository;

    public TxOutController(
      ICourtOrderRepository courtOrderRepository)
    {
      _courtOrderRepository = courtOrderRepository ?? throw new ArgumentNullException(nameof(courtOrderRepository));
    }

    [HttpGet("{TxId}/{Vout}")]
    public async Task<ActionResult<FundViewModel>> GetAsync([FromRoute] TxOutViewModelGet tx)
    {
      var result = await _courtOrderRepository.QueryFundByTxOutAsync(tx.TxId, tx.Vout);
      if (result == null)
      {
        return NotFound();
      }
      return Ok(new FundViewModel(result));
    }
  }
}