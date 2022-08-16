// Copyright (c) 2020 Bitcoin Association

using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BMDomain = BlacklistManager.Domain.Models;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class EnforceAtHeight
  {
    [TestMethod]
    public void EnforceAtHeightList_AreSameIntervals()
    {
      var eahList1 = new BlacklistManager.Domain.Models.EnforceAtHeightList();
      var eahList2 = new BlacklistManager.Domain.Models.EnforceAtHeightList();

      Assert.IsTrue(BlacklistManager.Domain.Models.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2), "Empty lists should be equal");

      eahList1.Add(new Domain.Models.EnforceAtHeight("h1", -1, -1, false));

      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2), "Empty list and not empty list should not be equal");
      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1), "Empty list and not empty list should not be equal");

      eahList2.Add(new Domain.Models.EnforceAtHeight("h1", -1, -1, false));

      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2));
      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1));

      eahList1.Add(new Domain.Models.EnforceAtHeight("h2", 100, -1, false));

      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2));
      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1));

      eahList2.Add(new Domain.Models.EnforceAtHeight("h2", 100, -1, false));

      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2));
      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1));

      eahList1.Add(new Domain.Models.EnforceAtHeight("h2", 100, -1, true));
      eahList1.Add(new Domain.Models.EnforceAtHeight("h3", 100, -1, true));

      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2), "Same intervals with different attributes should be equal");
      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1), "Same intervals with different attributes should be equal");

      eahList1.Add(new Domain.Models.EnforceAtHeight("h2", 100, 200, true));
      eahList2.Add(new Domain.Models.EnforceAtHeight("h2", 100, 201, true));

      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList2));
      Assert.IsFalse(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList2, eahList1));

      Assert.IsTrue(BMDomain.EnforceAtHeightList.AreSameIntervals(eahList1, eahList1), "Same list instances should be equal");
    }

    [TestMethod]
    public void EnforceAtHeight_StatusCheck()
    {
      var eah = new BMDomain.EnforceAtHeight("h1", -1, -1, false);
      Assert.IsFalse(eah.IsConsensusFrozen);
      Assert.IsTrue(eah.IsPolicyFrozen);
      Assert.IsFalse(eah.IsSpendable);

      eah = new BMDomain.EnforceAtHeight("h1", -1, 100, true);
      Assert.IsFalse(eah.IsConsensusFrozen);
      Assert.IsFalse(eah.IsPolicyFrozen);
      Assert.IsTrue(eah.IsSpendable);

      eah = new BMDomain.EnforceAtHeight("h1", -1, -1, true);
      Assert.IsFalse(eah.IsConsensusFrozen);
      Assert.IsFalse(eah.IsPolicyFrozen);
      Assert.IsTrue(eah.IsSpendable);

      eah = new BMDomain.EnforceAtHeight("h1", 100, -1, false);
      Assert.IsTrue(eah.IsConsensusFrozen);
      Assert.IsFalse(eah.IsPolicyFrozen);
      Assert.IsFalse(eah.IsSpendable);

      eah = new BMDomain.EnforceAtHeight("h1", 100, -1, true);
      Assert.IsTrue(eah.IsConsensusFrozen);
      Assert.IsFalse(eah.IsPolicyFrozen);
      Assert.IsFalse(eah.IsSpendable);

      eah = new BMDomain.EnforceAtHeight("h1", 100, 200, true);
      Assert.IsTrue(eah.IsConsensusFrozen);
      Assert.IsFalse(eah.IsPolicyFrozen);
      Assert.IsFalse(eah.IsSpendable);

      Assert.ThrowsException<BadRequestException>(() => eah = new BMDomain.EnforceAtHeight("h1", 100, 200, false), "If stopAtHeight is defined then unfreeze order must exist");
      Assert.ThrowsException<BadRequestException>(() => eah = new BMDomain.EnforceAtHeight("h1", -1, 200, false), "If stopAtHeight is defined then unfreeze order must exist");
    }

    [TestMethod]
    public void EnforceAtHeightList_StatusCheck()
    {
      var eahList = new BlacklistManager.Domain.Models.EnforceAtHeightList();

      Assert.IsFalse(eahList.ContainsIsConsensusFrozen);
      Assert.IsFalse(eahList.ContainsIsPolicyFrozen);
      Assert.IsFalse(eahList.ContainsIsSpendable);

      eahList.Add(new Domain.Models.EnforceAtHeight("h1", -1, -1, false));

      Assert.IsFalse(eahList.ContainsIsConsensusFrozen);
      Assert.IsTrue(eahList.ContainsIsPolicyFrozen);
      Assert.IsFalse(eahList.ContainsIsSpendable);

      eahList.Add(new Domain.Models.EnforceAtHeight("h1", -1, -1, true));

      Assert.IsFalse(eahList.ContainsIsConsensusFrozen);
      Assert.IsTrue(eahList.ContainsIsPolicyFrozen);
      Assert.IsTrue(eahList.ContainsIsSpendable);

      eahList.Add(new Domain.Models.EnforceAtHeight("h1", 100, -1, false));

      Assert.IsTrue(eahList.ContainsIsConsensusFrozen);
      Assert.IsTrue(eahList.ContainsIsPolicyFrozen);
      Assert.IsTrue(eahList.ContainsIsSpendable);

      eahList = new BlacklistManager.Domain.Models.EnforceAtHeightList();
      eahList.Add(new Domain.Models.EnforceAtHeight("h1", -1, -1, false));
      eahList.Add(new Domain.Models.EnforceAtHeight("h1", -1, 100, true));

      Assert.IsFalse(eahList.ContainsIsConsensusFrozen);
      Assert.IsTrue(eahList.ContainsIsPolicyFrozen);
      Assert.IsTrue(eahList.ContainsIsSpendable);

      eahList.Add(new Domain.Models.EnforceAtHeight("h1", 100, 100, true));

      Assert.IsTrue(eahList.ContainsIsConsensusFrozen);
      Assert.IsTrue(eahList.ContainsIsPolicyFrozen);
      Assert.IsTrue(eahList.ContainsIsSpendable);
    }
  }
}
