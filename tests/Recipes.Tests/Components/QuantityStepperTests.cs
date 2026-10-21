using Bunit;
using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Components;

public class QuantityStepperTests : BunitContext
{
    private readonly List<int> changes = new();

    private IRenderedComponent<QuantityStepper> RenderStepper(int value, int min = 1, int max = 10) =>
        Render<QuantityStepper>(p => p
            .Add(s => s.Label, "Batches of Pancakes")
            .Add(s => s.Value, value)
            .Add(s => s.Min, min)
            .Add(s => s.Max, max)
            .Add(s => s.ValueChanged, v => changes.Add(v)));

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<QuantityStepper> cut, string label) =>
        cut.Find($"button[aria-label={label}]");

    [Fact]
    public void ShowsTheValue_InALabelledGroup()
    {
        var cut = RenderStepper(3);

        cut.Find(".stepper-value").TextContent.ShouldBe("3×");
        cut.Find("[role=group]").GetAttribute("aria-label").ShouldBe("Batches of Pancakes");
    }

    [Fact]
    public void Buttons_RaiseOneUpOrOneDown()
    {
        var cut = RenderStepper(3);

        Button(cut, "Increase").Click();
        Button(cut, "Decrease").Click();

        changes.ShouldBe(new[] { 4, 2 });
    }

    [Fact]
    public void AtTheMinimum_DecreaseIsDisabled()
    {
        var cut = RenderStepper(1);

        Button(cut, "Decrease").HasAttribute("disabled").ShouldBeTrue();
        Button(cut, "Increase").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void AtTheMaximum_IncreaseIsDisabled()
    {
        var cut = RenderStepper(10);

        Button(cut, "Increase").HasAttribute("disabled").ShouldBeTrue();
        Button(cut, "Decrease").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void AValueOutsideTheRange_IsBroughtBackInsideIt()
    {
        var cut = RenderStepper(15, max: 10);

        Button(cut, "Decrease").Click();   // 14, clamped to the maximum

        changes.ShouldBe(new[] { 10 });
    }
}
