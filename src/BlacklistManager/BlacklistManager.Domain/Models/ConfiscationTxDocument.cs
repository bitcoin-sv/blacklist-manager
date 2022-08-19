// Copyright (c) 2020 Bitcoin Association

using Common;
using Common.SmartEnums;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Common.Consts;

namespace BlacklistManager.Domain.Models
{
  public class ConfiscationTxDocument
  {
    public DocumentType DocumentType { get; init; }

    public string ConfiscationCourtOrderId { get; init; }

    public string ConfiscationCourtOrderHash { get; init; }

    public List<ConfiscationTx> ConfiscationTxs { get; init; }

    List<(string TxId, int? EnforceAtHeight, byte[] payload)> _confiscationTxData;
    public IReadOnlyCollection<(string TxId, int? EnforceAtHeight, byte[] payload)> ConfiscationTxData => _confiscationTxData;

    public DateTime? CreatedAt { get; init; }

    public DateTime SignedDate { get; init; }

    private void AddConfiscationTx(string txId, byte[] bytes)
    {
      if (_confiscationTxData == null)
      {
        _confiscationTxData = new List<(string, int?, byte[])>();
      }
      _confiscationTxData.Add((txId, null, bytes));
    }

    public (bool IsSuccess, string[] Errors) Validate(CourtOrder courtOrder, Network network)
    {
      if (string.IsNullOrEmpty(courtOrder.Destination.Address))
      {
        return (false, new[] { "Destination address on court order cannot be null." });
      }
      if (ConfiscationTxs == null || ConfiscationTxs.Count == 0)
      {
        return (false, new[] { "Confiscation transactions list is empty." });
      }
      if (ConfiscationCourtOrderHash != courtOrder.CourtOrderHash)
      {
        return (false, new[] { "Court order hash on ConfiscationTxDocument doesn't match hash on the order." });
      }

      var errorList = new List<string>();
      var funds2Confiscate = new HashSet<Fund>(courtOrder.Funds, new FundEqualityComparerByTxOut());
      var confiscationFunds = new HashSet<Fund>(new FundEqualityComparerByTxOut());
      long confiscationAmountFromTxs = 0L;
      var blockChainType = courtOrder.Blockchain.Split("-")[0];

      foreach (var confiscationTx in ConfiscationTxs)
      {
        var txHex = confiscationTx.Hex;
        if (!Transaction.TryParse(confiscationTx.Hex, network, out var tx))
        {
          errorList.Add($"Unable to load transaction. Starting hex {confiscationTx.GetHexStartingPart()}");
        }

        var txFunds = new HashSet<Fund>(tx.Inputs.Select(x => new Fund(x.PrevOut.Hash.ToString(), x.PrevOut.N, -1, -1)), new FundEqualityComparerByTxOut());
        if (confiscationFunds.Overlaps(txFunds))
        {
          errorList.Add($"Funds being spent by transaction {tx.GetHash()} are already being spent by another transaction in the collection of transactions (double spend detected).");
        }
        confiscationFunds.UnionWith(txFunds);

        bool confiscationProtocolIdPresent = false;
        bool addressMatched = false;

        foreach (var output in tx.Outputs)
        {
          var ops = output.ScriptPubKey.ToOps().ToArray();
          bool opReturnFound = false;
          int opCodeIndex = 0;

          if (blockChainType == BlockChainType.BitcoinSV &&
              ops[0].Code == OpcodeType.OP_FALSE &&
              ops[1].Code == OpcodeType.OP_RETURN)
          {
            opReturnFound = true;
            opCodeIndex = 2;
          }
          else if (blockChainType == BlockChainType.BitcoinCore &&
                   ops[0].Code == OpcodeType.OP_RETURN)
          {
            opReturnFound = true;
            opCodeIndex = 1;
          }

          if (opReturnFound)
          {
            var pushData = ops[opCodeIndex++].PushData;
            if (pushData == null || HelperTools.ByteToHexString(pushData) != Consts.ConfiscationProtocolId)
            {
              continue;
            }
            confiscationProtocolIdPresent = true;
            var txPayload = new MemoryStream(ops[opCodeIndex].PushData);
            var version = HelperTools.ByteToHexString(txPayload.ReadBytes(1));
            if (version == Consts.ConfiscationProtocolVersion)
            {
              var refCourtOrderHash = new uint256(txPayload.ReadBytes(32), lendian: false);
              if (refCourtOrderHash != new uint256(courtOrder.CourtOrderHash))
              {
                errorList.Add($"Transaction '{tx.GetHash()}' is referencing court order with hash '{refCourtOrderHash}' instead of '{courtOrder.CourtOrderHash}'.");
              }
            }
            else
            {
              errorList.Add("Unsupported version number for confiscation protocol.");
            }
          }
          else
          {
            confiscationAmountFromTxs += output.Value.Satoshi;
          }
          if (output.ScriptPubKey?.GetDestinationAddress(network)?.ToString() == courtOrder.Destination.Address)
          {
            addressMatched = true;
          }
        }
        if (!confiscationProtocolIdPresent)
        {
          errorList.Add($"Wrong protocol id for confiscation transaction '{tx.GetHash()}'.");
        }
        if (!addressMatched)
        {
          errorList.Add($"Destination address for confiscation transaction '{tx.GetHash()}' does not match the address on court order.");
        }

        AddConfiscationTx(tx.GetHash().ToString(), tx.ToBytes());
      }

      if (!funds2Confiscate.SetEquals(confiscationFunds))
      {
        if (confiscationFunds.Count < funds2Confiscate.Count)
        {
          errorList.Add("Not all funds marked for confiscation are being confiscated.");
        }
        else
        {
          errorList.Add($"Funds are trying to be spent that are not frozen.");
        }
      }

      if (confiscationAmountFromTxs > courtOrder.Destination.Amount)
      {
        errorList.Add("Sum of confiscated value on confiscation transactions is greater than the confiscation amount on court order.");
      }
      return (!errorList.Any(), errorList.ToArray());
    }
  }
}
