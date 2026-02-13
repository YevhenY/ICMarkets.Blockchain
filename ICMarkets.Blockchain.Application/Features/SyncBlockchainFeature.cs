using FluentValidation;
using ICMarkets.Blockchain.Domain.Interfaces;
using MediatR;

namespace ICMarkets.Blockchain.Application.Features
{
    public class SyncBlockchainCommand : IRequest<SyncResult>
    {
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public int Count { get; set; }
    }

    public class SyncBlockchainHandler : IRequestHandler<SyncBlockchainCommand, SyncResult>
    {
        private readonly IBlockCypherService _service;
        private readonly IRepository _repository;

        public SyncBlockchainHandler(IBlockCypherService service, IRepository repository)
        {
            _service = service;
            _repository = repository;
        }

        public async Task<SyncResult> Handle(SyncBlockchainCommand request, CancellationToken cancellationToken)
        {
            var data = await _service.FetchAllAsync(cancellationToken);

            if (data.Any())
                await _repository.AddRangeAsync(data, cancellationToken);

            return new SyncResult { Success = true, Count = data.Count };
        }
    }

    public class SyncBlockchainValidator : AbstractValidator<SyncBlockchainCommand>
    {
        public SyncBlockchainValidator()
        {
        }
    }
}
