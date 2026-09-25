using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VehicleRental.Api.Middleware;

/// <summary>
/// Valida os argumentos da action com o <see cref="IValidator{T}"/> registrado, se existir.
/// Feito à mão em vez de usar a auto-validação do FluentValidation.AspNetCore, que está
/// descontinuada: a falha vira uma ValidationException, que o middleware traduz em 400.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        await next();
    }
}
