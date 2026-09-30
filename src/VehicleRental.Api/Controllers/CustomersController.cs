using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleRental.Api.Extensions;
using VehicleRental.Application.Customers;
using VehicleRental.Application.Customers.Dtos;

namespace VehicleRental.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = Roles.Customer)]
[Produces("application/json")]
public sealed class CustomersController(CustomerService customerService) : ControllerBase
{
    /// <summary>Retorna o perfil do cliente autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerResponse>> GetMe(CancellationToken cancellationToken)
        => Ok(await customerService.GetMeAsync(User.ToCurrentUser(), cancellationToken));

    [HttpPut("me")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> UpdateMe(
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
        => Ok(await customerService.UpdateMeAsync(User.ToCurrentUser(), request, cancellationToken));
}
