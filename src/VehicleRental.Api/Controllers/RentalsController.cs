using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleRental.Api.Extensions;
using VehicleRental.Application.Rentals;
using VehicleRental.Application.Rentals.Dtos;

namespace VehicleRental.Api.Controllers;

[ApiController]
[Route("api/rentals")]
[Authorize]
[Produces("application/json")]
public sealed class RentalsController(RentalService rentalService) : ControllerBase
{
    /// <summary>Cria uma reserva para o cliente autenticado.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    [ProducesResponseType<RentalResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RentalResponse>> Create(
        CreateRentalRequest request,
        CancellationToken cancellationToken)
    {
        var rental = await rentalService.CreateAsync(User.ToCurrentUser(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = rental.Id }, rental);
    }

    /// <summary>Admin recebe todos os aluguéis; cliente recebe apenas os próprios.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RentalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalResponse>>> List(CancellationToken cancellationToken)
        => Ok(await rentalService.ListAsync(User.ToCurrentUser(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RentalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RentalResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await rentalService.GetByIdAsync(User.ToCurrentUser(), id, cancellationToken));

    /// <summary>Registra a devolução e calcula a multa por atraso, se houver.</summary>
    [HttpPost("{id:guid}/return")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<RentalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RentalResponse>> Return(
        Guid id,
        ReturnRentalRequest request,
        CancellationToken cancellationToken)
        => Ok(await rentalService.ReturnAsync(User.ToCurrentUser(), id, request, cancellationToken));

    /// <summary>Cancela uma reserva ainda não iniciada.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<RentalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RentalResponse>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await rentalService.CancelAsync(User.ToCurrentUser(), id, cancellationToken));
}
