using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;

namespace BlacklistManager.API.Rest
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync();
  }
}
