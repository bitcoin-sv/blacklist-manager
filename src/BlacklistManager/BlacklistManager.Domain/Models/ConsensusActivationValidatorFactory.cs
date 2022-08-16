// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Repositories;

namespace BlacklistManager.Domain.Models
{
  public class ConsensusActivationValidatorFactory : IConsensusActivationValidatorFactory
  {
    private readonly ITrustListRepository trustListRepository;
    private readonly ICourtOrderRepository courtOrderRepository;

    public ConsensusActivationValidatorFactory(
      ITrustListRepository trustListRepository,
      ICourtOrderRepository courtOrderRepository)
    {
      this.trustListRepository = trustListRepository;
      this.courtOrderRepository = courtOrderRepository;
    }

    public IConsensusActivationValidator Create(ConsensusActivation consensusActivation, string courtOrderHash)
    {
      return new ConsensusActivationValidator(consensusActivation, courtOrderHash, trustListRepository, courtOrderRepository);
    }
  }
}
