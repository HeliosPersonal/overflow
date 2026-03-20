using CommandFlow;
using Overflow.EstimationService.DTOs;
using Overflow.EstimationService.Models;

namespace Overflow.EstimationService.Features.Rooms.Queries;

public record GetDecksQuery : IQuery<IEnumerable<DeckDefinitionResponse>>;

public class GetDecksHandler : IRequestHandler<GetDecksQuery, IEnumerable<DeckDefinitionResponse>>
{
    public Task<IEnumerable<DeckDefinitionResponse>> Handle(GetDecksQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<DeckDefinitionResponse> decks = Decks.All.Values
            .Select(d => new DeckDefinitionResponse(d.Id, d.Name, d.Values));
        return Task.FromResult(decks);
    }
}