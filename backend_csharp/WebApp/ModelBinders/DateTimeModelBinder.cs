using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebApp.ModelBinders;

/// <summary>
/// Custom model binder for DateTime that parses datetime-local HTML input values
/// (format: yyyy-MM-ddTHH:mm) using InvariantCulture, bypassing locale-based parsing.
/// </summary>
public class InvariantDateTimeModelBinder : IModelBinder
{
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd"
    ];

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (string.IsNullOrEmpty(value))
        {
            // For non-nullable DateTime, mark as failed
            if (bindingContext.ModelMetadata.IsRequired)
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "A date and time value is required.");
            }
            return Task.CompletedTask;
        }

        if (DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
        {
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        else if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out result))
        {
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{value}' is not a valid date and time.");
        }

        return Task.CompletedTask;
    }
}

public class InvariantDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(DateTime) ||
            context.Metadata.ModelType == typeof(DateTime?))
        {
            return new InvariantDateTimeModelBinder();
        }
        return null;
    }
}
