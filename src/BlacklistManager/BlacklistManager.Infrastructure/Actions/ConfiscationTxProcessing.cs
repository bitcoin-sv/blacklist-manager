// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.Bitcoin;
using Common.BitcoinRpcClient.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class ConfiscationTxProcessing : IConfiscationTxProcessing
  {
    readonly ICourtOrderRepository _courtOrderRepository;
    readonly IBitcoinFactory _bitcoinFactory;
    readonly ICourtOrders _courtOrders;
    readonly IMetrics _metrics;
    readonly ILogger<ConfiscationTxProcessing> _logger;
    readonly AppSettings _appSettings;
    HashSet<(long BlockHeight, string BlockHash)> _blockHashSet;

    public ConfiscationTxProcessing(
      ICourtOrderRepository courtOrderRepository, 
      IBitcoinFactory bitcoinFactory, 
      IOptions<AppSettings> options, 
      ICourtOrders courtOrders,
      IMetrics metrics,
      ILogger<ConfiscationTxProcessing> logger)
    {
      _bitcoinFactory = bitcoinFactory;
      _courtOrderRepository = courtOrderRepository;
      _courtOrders = courtOrders;
      _logger = logger;
      _appSettings = options.Value;
      _metrics = metrics;

      _blockHashSet = new(_appSettings.BlockHashCollectionSize + 10);
    }

    public async Task<bool> SendConfiscationTransactionsAsync(Node node, CancellationToken cancellationToken, long? forceResendLength = null)
    {
      if (forceResendLength == 0)
      {
        return true;
      }
      try
      {
        var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);

        var nodeBlockCount = await bitcoind.GetBlockCountAsync(); 
        var heightForSubmit = nodeBlockCount + 1;// we add 1, because tx can be present in mempool 1 block before EAH
        var minEAT = forceResendLength.HasValue ? heightForSubmit - forceResendLength.Value : heightForSubmit - _appSettings.TxResubmitionPeriodInBlocks;
        var confiscationTxs2Send = (await _courtOrderRepository.GetConfiscationTransactionsAsync((int)minEAT, (int)heightForSubmit, forceResendLength.HasValue)).ToList();

        if (!confiscationTxs2Send.Any())
        {
          return true;
        }

        var whiteList = await bitcoind.QueryConfiscationTxidWhitelistAsync(cancellationToken);

        var notOnWhiteList = confiscationTxs2Send.Where(x => !x.RewardTransaction && !whiteList.ConfiscationTxs.Exists(y => y.ConfiscationTx.TxId == x.TxId));
        if (notOnWhiteList.Any())
        {
          _logger.LogWarning($"{notOnWhiteList.Count()} transactions are not present on the whitelist for node {node} and will be skipped.");
          confiscationTxs2Send.RemoveAll(x => notOnWhiteList.Any(y => x.TxId == y.TxId));
        }

        //if our txs are already in mempool remove them from the list
        var mempoolTxs = await bitcoind.GetRawMempoolAsync();
        confiscationTxs2Send.RemoveAll(x => mempoolTxs.Any(y => x.TxId == y));

        if (!confiscationTxs2Send.Any())
        {
          return true;
        }


        var txHexArray = confiscationTxs2Send.Select(x => (x.TxId, HelperTools.ByteToHexString(x.Body))).ToArray();
        // On each call we will resubmit all confiscation transactions for certain period so we will always get some already known errors
        var rpcResult = (await bitcoind.SendRawTransactionsAsync(txHexArray, cancellationToken));
        var rpcErrors = rpcResult.Where(x => x.ErrorCode != (int)SendTransactionErrorCode.AlreadyKnown).ToList();
        rpcErrors.ForEach(x => x.ErrorAtHeight = (int)nodeBlockCount);

        var result = confiscationTxs2Send.Where(x => !rpcResult.Any(y => x.TxId == y.TxId))
                                          .Select(x => new SendRawTransactionsResult { TxId = x.TxId, SubmittedAtHeight = (int)nodeBlockCount, ErrorCode = null, ErrorDescription = null });

        _metrics.SubmittedTxs.Add(result.Count(x => x.SubmittedAtHeight.HasValue));
        if (result.Count() > 0)
        {
          _logger.LogInformation($"Successfully submitted '{result.Count(x => x.SubmittedAtHeight.HasValue)}' confiscation/reward transactions. ");
        }
        if (!forceResendLength.HasValue)
        {
          _metrics.RejectedTxs.Add(rpcErrors.Count);
        }
        result = result.Concat(rpcErrors);
        await _courtOrderRepository.SetTransactionErrorsAsync(result.ToArray());

        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error while submitting transactions to node {node.ToExternalId()}");
        return false;
      }
    }

    public async Task<bool> SubmitWhitelistTxIdsAsync(Node[] nodes, CancellationToken cancellationToken)
    {
      bool processingSuccessful = true;
      var policyConfiscationOrders = await _courtOrderRepository.GetCourtOrderForConfiscationTxWhiteListAsync();

      foreach (var order in policyConfiscationOrders)
      {
        bool orderSuccessfull = false;

        foreach (var node in nodes)
        {
          if (cancellationToken.IsCancellationRequested)
          {
            processingSuccessful = false;
            break;
          }
          var confiscationTxs = await _courtOrderRepository.GetConfiscationTransactionsForWhiteListAsync(order.CourtOrderHash, (int)node.Id);

          if (!confiscationTxs.Any())
          {
            continue;
          }

          var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);
          var rpcConfiscation = new RpcConfiscation
          {
            ConfiscationTxs = confiscationTxs.Select(x => new RpcConfiscationTx
            {
              ConfiscationTxData = new RpcConfiscationTxData
              {
                Hex = HelperTools.ByteToHexString(x.Body),
                EnforceAtHeight = x.EnforceAtHeight
              }
            }).ToArray()
          };

          Common.BitcoinRpcClient.Responses.RpcConfiscationResult result = null;
          try
          {
            result = await bitcoind.AddToConfiscationTxIdWhiteListAsync(rpcConfiscation, cancellationToken);
          }
          catch (Exception ex)
          {
            _logger.LogError($"Unable to update whitelist for '{order.CourtOrderHash}' while caling node {node} . Reason: {ex.GetBaseException().Message}");
            processingSuccessful = false;
            continue;
          }

          if (result?.NotProcessed is null || !result.NotProcessed.Any())
          {
            _logger.LogInformation($"Whitelist for courtOrder '{order.CourtOrderHash}' successfully submited to node '{node}'");
            await _courtOrderRepository.InsertWhitelistedNodeInfoAsync(order.CourtOrderHash, (int)node.Id);
            orderSuccessfull |= true;
          }
          else if (result.NotProcessed.Any())
          {
            processingSuccessful = false;
            foreach (var error in result.NotProcessed)
            {
              _logger.LogError($"TransactionId '{error.ConfiscationTx.TxId}' was not whitelisted by node {node}. Reason: {error.Reason}");
            }
          }
        }

        if (orderSuccessfull)
        {
          await _courtOrders.SetCourtOrderStatusAsync(order.CourtOrderHash, CourtOrderStatus.ConfiscationConsensusWhitelisted, order.EnforceAtHeight);
        }
      }
      return processingSuccessful;
    }

    private async Task<long> CheckBlockChain4ReorgAsync(IBitcoinRpc bitcoind, CancellationToken cancellationToken)
    {
      var bestBlockHash = await bitcoind.GetBestBlockHashAsync();
      var blockHeader = await bitcoind.GetBlockHeaderAsync(bestBlockHash, cancellationToken);

      //Best block hash is already in present in the set, nothing to do
      if (_blockHashSet.Contains((blockHeader.Height, blockHeader.Hash.ToString())))
      {
        return 0;
      }

      // All block hashes up to the last one are present so we insert the new one and exit
      if (_blockHashSet.Contains((blockHeader.Height - 1, blockHeader.Previousblockhash)))
      {
        if (_blockHashSet.Count == _appSettings.BlockHashCollectionSize)
        {
          _blockHashSet.Remove(_blockHashSet.OrderBy(x => x.BlockHeight).First());
        }
        _blockHashSet.Add(new(blockHeader.Height, blockHeader.Hash.ToString()));
        return 0;
      }

      // Reorg occured or multiple blocks were mined from last check
      var missingBlockHashes = new HashSet<(long BlockHeight, string BlockHash)>
      {
        (blockHeader.Height, blockHeader.Hash.ToString())
      };

      do
      {
        if (blockHeader.Previousblockhash == null)
        {
          break;
        }
        blockHeader = await bitcoind.GetBlockHeaderAsync(blockHeader.Previousblockhash, cancellationToken);
        missingBlockHashes.Add((blockHeader.Height, blockHeader.Hash.ToString()));
      }
      while (!_blockHashSet.Contains((blockHeader.Height - 1, blockHeader.Previousblockhash)) && missingBlockHashes.Count < _appSettings.BlockHashCollectionSize);

      var minHeight = missingBlockHashes.Min(x => x.BlockHeight);
      _blockHashSet.RemoveWhere(x => x.BlockHeight >= minHeight && x.BlockHeight < (minHeight + missingBlockHashes.Count));
      _blockHashSet = _blockHashSet.Concat(missingBlockHashes).ToHashSet();

      // Remove excessive items
      while (_blockHashSet.Count > _appSettings.BlockHashCollectionSize)
      {
        _blockHashSet.Remove(_blockHashSet.OrderBy(x => x.BlockHeight).First());
      }

      return missingBlockHashes.Count;
    }

    public async Task<bool> ConfiscationsInBlockCheckAsync(Node node, CancellationToken cancellationToken)
    {
      var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);
      var reorgSize = await CheckBlockChain4ReorgAsync(bitcoind, cancellationToken);

      return await SendConfiscationTransactionsAsync(node, cancellationToken, reorgSize);
    }
  }
}
