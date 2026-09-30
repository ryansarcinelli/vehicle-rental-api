using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleRental.Application.Common;
using VehicleRental.Application.Vehicles;
using VehicleRental.Application.Vehicles.Dtos;

namespace VehicleRental.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize]
[Produces("application/json")]
public sealed class VehiclesController(VehicleService vehicleService) : ControllerBase
{
    /// <summary>Lista a frota com filtros e paginação.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<VehicleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VehicleResponse>>> Search(
        [FromQuery] VehicleQuery query,
        CancellationToken cancellationToken)
        => Ok(await vehicleService.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await vehicleService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleResponse>> Create(
        CreateVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var vehicle = await vehicleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicle);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleResponse>> Update(
        Guid id,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken)
        => Ok(await vehicleService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Envia para manutenção ou libera de volta para locação.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<VehicleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleResponse>> ChangeStatus(
        Guid id,
        ChangeVehicleStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await vehicleService.ChangeStatusAsync(id, request.Status, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await vehicleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
