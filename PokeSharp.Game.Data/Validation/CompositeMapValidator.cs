using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Composite validator that runs multiple validators in sequence.
///     Implements Composite Pattern for flexible validation composition.
/// </summary>
public class CompositeMapValidator : IMapValidator
{
    private readonly List<IMapValidator> _validators = new();

    public CompositeMapValidator() { }

    public CompositeMapValidator(params IMapValidator[] validators)
    {
        _validators.AddRange(validators);
    }

    /// <summary>
    ///     Validates the map using all registered validators.
    ///     Aggregates all errors and warnings from all validators.
    /// </summary>
    public ValidationResult Validate(TmxDocument map, string mapPath)
    {
        var aggregateResult = new ValidationResult();

        foreach (IMapValidator validator in _validators)
        {
            ValidationResult result = validator.Validate(map, mapPath);

            // Aggregate errors and warnings
            foreach (ValidationError error in result.Errors)
            {
                aggregateResult.AddError($"[{validator.GetType().Name}] {error}");
            }

            foreach (ValidationWarning warning in result.Warnings)
            {
                aggregateResult.AddWarning($"[{validator.GetType().Name}] {warning}");
            }
        }

        return aggregateResult;
    }

    /// <summary>
    ///     Adds a validator to the composite.
    /// </summary>
    public void AddValidator(IMapValidator validator)
    {
        _validators.Add(validator);
    }

    /// <summary>
    ///     Creates a default validator with all standard validation rules.
    /// </summary>
    public static CompositeMapValidator CreateDefault()
    {
        return new CompositeMapValidator(
            new MapDimensionsValidator(),
            new TilesetValidator(),
            new LayerValidator()
        );
    }
}
