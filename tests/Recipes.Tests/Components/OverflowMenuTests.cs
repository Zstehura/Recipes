using Bunit;
using Microsoft.AspNetCore.Components;
using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Components;

public class OverflowMenuTests : BunitContext
{
    private int chosen;

    public OverflowMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // FocusAsync goes through JS interop
    }

    private IRenderedComponent<OverflowMenu> RenderMenu() => Render<OverflowMenu>(p => p
        .Add(m => m.Label, "Recipe actions")
        .Add(m => m.ChildContent, (RenderFragment)(b =>
        {
            b.OpenElement(0, "button");
            b.AddAttribute(1, "class", "item");
            b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => chosen++));
            b.AddContent(3, "Delete recipe");
            b.CloseElement();
        })));

    private static string Expanded(IRenderedComponent<OverflowMenu> cut) =>
        cut.Find("button[aria-label='Recipe actions']").GetAttribute("aria-expanded")!;

    [Fact]
    public void StartsClosed()
    {
        var cut = RenderMenu();

        Expanded(cut).ShouldBe("false");
        cut.FindAll(".item").ShouldBeEmpty();
    }

    [Fact]
    public void Button_OpensTheMenu()
    {
        var cut = RenderMenu();

        cut.Find("button[aria-label='Recipe actions']").Click();

        Expanded(cut).ShouldBe("true");
        cut.FindAll(".item").Count.ShouldBe(1);
        cut.Find(".overflow-menu-items").Id.ShouldBe(
            cut.Find("button[aria-label='Recipe actions']").GetAttribute("aria-controls"));
    }

    [Fact]
    public void ChoosingAnItem_RunsItAndCloses()
    {
        var cut = RenderMenu();
        cut.Find("button[aria-label='Recipe actions']").Click();

        cut.Find(".item").Click();

        chosen.ShouldBe(1);
        Expanded(cut).ShouldBe("false");
    }

    [Fact]
    public void TappingOutside_Closes()
    {
        var cut = RenderMenu();
        cut.Find("button[aria-label='Recipe actions']").Click();

        cut.Find(".overflow-menu-backdrop").Click();

        Expanded(cut).ShouldBe("false");
        chosen.ShouldBe(0);
    }

    [Fact]
    public void Escape_Closes()
    {
        var cut = RenderMenu();
        cut.Find("button[aria-label='Recipe actions']").Click();

        cut.Find(".overflow-menu").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Expanded(cut).ShouldBe("false");
    }
}
