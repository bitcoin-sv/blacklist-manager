// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Test.Functional.MockServices;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Test.Functional.Server
{
  public class BlacklistManagerTestsStartup : API.Rest.Startup
  {
    public BlacklistManagerTestsStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {
    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);

      string blackListManagerConnectionString = Configuration["BlacklistManagerConnectionStrings:DBConnectionString"];

      services.AddSingleton<IBitcoindFactory, BitcoindFactoryMock>();
      services.AddSingleton<ILegalEntityFactory, LegalEntityFactoryMock>();
      services.AddSingleton<IConsensusActivationValidatorFactory, ConsensusActivationValidatorFactoryMock>();
      services.AddSingleton<IBackgroundJobs, BackgroundJobsMock>();
      services.AddSingleton<ICourtOrderRepository, CourtOrderRepositoryPostgres>(sp =>
        new CourtOrderRepositoryPostgres(
          blackListManagerConnectionString,
          sp.GetRequiredService<ILoggerFactory>()));
      services.AddSingleton<ITrustListRepository>(x => new TrustListRepositoryPostgres(blackListManagerConnectionString));
      services.AddSingleton<INodeRepository>(x => new NodeRepositoryPostgres(blackListManagerConnectionString));
      services.AddSingleton<ILegalEntityRepository>(x => new LegalEntityRepositoryPostgres(blackListManagerConnectionString));
      services.AddSingleton<IDelegatedKeyRepositoryPostgres>(x => new DelegatedKeyRepositoryPostgres(blackListManagerConnectionString, x.GetRequiredService<ILoggerFactory>()));

      // register IPropagationEvents
      services.AddSingleton<IPropagationEvents, PropagationEventsMock>();
    }
  }
}
