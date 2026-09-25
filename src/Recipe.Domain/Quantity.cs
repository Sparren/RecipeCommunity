namespace Recipe.Domain;

public sealed record Quantity
{
    public decimal? Value { get; private set; }
    public decimal? MaximumValue { get; private set; }

    private Quantity() { }

    public Quantity(decimal? value, decimal? maximumValue = null)
    {
        if (value is <= 0 || maximumValue is <= 0)
            throw new DomainException("Quantity values must be positive.");
        if (maximumValue.HasValue && (!value.HasValue || maximumValue <= value))
            throw new DomainException("A range maximum must be greater than its starting value.");
        Value = value;
        MaximumValue = maximumValue;
    }
}

public sealed record Yield
{
    public Quantity Quantity { get; private set; } = new(null);
    public Guid UnitId { get; private set; }
    private Yield() { }

    public Yield(Quantity quantity, Guid unitId)
    {
        if (quantity.Value is null || unitId == Guid.Empty)
            throw new DomainException("Yield requires a numeric quantity and unit.");
        Quantity = quantity;
        UnitId = unitId;
    }
}

public sealed class DomainException(string message) : Exception(message);
