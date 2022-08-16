// Copyright (c) 2020 Bitcoin Association

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Common.Test.Unit
{
  [TestClass]
  public class HelperToolsTests
  {
    [DataRow("example@example.com")]
    [TestMethod]
    public void MailValidatorReturnsValid(string email)
    {
      Assert.IsTrue(HelperTools.IsValidEmail(email));
    }

    [DataRow("example")]
    [DataRow("example@example.")]
    [DataRow("example@exam ple.com")]
    [DataRow("asd%40asd.com")]
    [DataRow("asd%40asd.com%0a%0d%1b[31mntapp % 1b[0m % 09 % 0914:09:43:374 % 20INJECTION")]
    [TestMethod]
    public void MailValidatorReturnsInvalid(string email)
    {
      Assert.IsFalse(HelperTools.IsValidEmail(email));
    }
  }
}
