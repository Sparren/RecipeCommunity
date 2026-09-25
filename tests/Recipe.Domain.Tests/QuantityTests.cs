using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Domain.Tests;

public class QuantityTests
{
    [Fact]
    public void Exact_fraction_range_and_unspecified_are_distinct()
    {
        Assert.Equal(0.5m, new Quantity(0.5m).Value);
        Assert.Null(new Quantity(null).Value);
        Assert.Equal(3m, new Quantity(2m, 3m).MaximumValue);
    }
    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(null, 2)]
    public void Invalid_quantities_are_rejected(int? value, int? maximum) =>
        Assert.Throws<DomainException>(() => new Quantity((decimal?)value, (decimal?)maximum));
    [Fact]
    public void Yield_supports_ranges_but_requires_a_value_and_unit()
    {
        var yield = new Yield(new(20, 24), Guid.NewGuid());
        Assert.Equal(24, yield.Quantity.MaximumValue);
        Assert.Throws<DomainException>(() => new Yield(new(null), Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Yield(new(2), Guid.Empty));
    }
    [Fact]
    public void Ingredient_amounts_require_unit_consistency()
    {
        var entry = new RecipeIngredient { IngredientId = Guid.NewGuid(), Quantity = new(null), AmountDescription = "to taste" };
        entry.Validate();
        entry.Quantity = new(2, 3);
        Assert.Throws<DomainException>(entry.Validate);
        entry.UnitId = Guid.NewGuid(); entry.Validate();
        entry.Quantity = new(null); Assert.Throws<DomainException>(entry.Validate);
    }
    [Fact]
    public void Publishing_requires_ingredients_and_steps_but_drafts_do_not()
    {
        var r = new RecipeEntity { AuthorId = Guid.NewGuid(), Name = "Test", RecipeTypeId = Guid.NewGuid(), Yield = new(new(4), Guid.NewGuid()), IngredientGroups = [new()] };
        r.Validate(); r.PublicationState = PublicationState.Published;
        Assert.Throws<DomainException>(r.Validate);
        r.IngredientGroups[0].Ingredients.Add(new() { IngredientId = Guid.NewGuid() });
        r.CookingSteps.Add(new() { Instruction = "Cook" }); r.Validate();
    }
}
