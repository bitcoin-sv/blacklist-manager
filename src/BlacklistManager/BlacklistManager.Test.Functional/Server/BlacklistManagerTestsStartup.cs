// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Test.Functional.MockServices;
using BlacklistManager.Domain.BackgroundJobs;
using Microsoft.AspNetCore.Hosting;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Repositories;
using BlacklistManager.Domain.Actions;
using Common.Bitcoin;

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

      services.AddSingleton<IBitcoinFactory, BitcoinFactoryMock>();
      services.AddSingleton<ILegalEntityFactory, LegalEntityFactoryMock>();
      services.AddSingleton<IConsensusActivationValidatorFactory, ConsensusActivationValidatorFactoryMock>();
      services.AddSingleton<IBackgroundJobs, BackgroundJobsMock>();
      services.AddSingleton<ICourtOrderRepository, CourtOrderRepositoryPostgres>();
      services.AddSingleton<ITrustListRepository, TrustListRepositoryPostgres>();
      services.AddSingleton<INodeRepository, NodeRepositoryPostgres>();
      services.AddSingleton<ILegalEntityRepository, LegalEntityRepositoryPostgres>();
      

      // register IPropagationEvents
      services.AddSingleton<IPropagationEvents, PropagationEventsMock>();
    }
  }
}
