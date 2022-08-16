// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class TxOutController : ControllerBase
  {
    private readonly IQueryAction queryAction;

    public TxOutController(
      IQueryAction queryAction)
    {
      this.queryAction = queryAction ?? throw new ArgumentNullException(nameof(queryAction));
    }

    [HttpGet("{TxId}/{Vout}")]
    public async Task<ActionResult<CourtOrderQuery.Fund>> GetAsync([FromRoute] TxOutViewModelGet tx)
    {
      var result = await queryAction.QueryFundByTxOutAsync(tx.TxId, tx.Vout);
      if (result == null)
      {
        return NotFound();
      }
      return Ok(result);
    }
  }
}