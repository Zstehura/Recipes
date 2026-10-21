using Bunit;
using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Components;

public class ConfirmDialogTests : BunitContext
{
    private int confirmed;
    private int cancelled;

    public ConfirmDialogTests()
    {
        JSInterop.SetupVoid("appDialog.show", _ => true);
        JSInterop.SetupVoid("appDialog.close", _ => true);
    }

    private IRenderedComponent<ConfirmDialog> RenderDialog(bool open) => Render<ConfirmDialog>(p => p
        .Add(d => d.Open, open)
        .Add(d => d.Title, "Delete this recipe?")
        .Add(d => d.ConfirmLabel, "Delete recipe")
        .Add(d => d.OnConfirm, () => confirmed++)
        .Add(d => d.OnCancel, () => cancelled++));

    [Fact]
    public void Closed_DoesNotShowTheModal()
    {
        RenderDialog(open: false);

        JSInterop.VerifyNotInvoke("appDialog.show");
    }

    [Fact]
    public void Open_ShowsTheModal_WithCancelFocused()
    {
        var cut = RenderDialog(open: true);

        JSInterop.VerifyInvoke("appDialog.show");
        cut.Find("button[autofocus]").TextContent.ShouldBe("Cancel");
        cut.Find("dialog").GetAttribute("aria-labelledby").ShouldBe(cut.Find("h2").Id);
    }

    [Fact]
    public void ConfirmButton_RaisesOnConfirm()
    {
        var cut = RenderDialog(open: true);

        cut.Find("button.btn-danger").Click();

        confirmed.ShouldBe(1);
        cancelled.ShouldBe(0);
    }

    [Fact]
    public void CancelButton_RaisesOnCancel()
    {
        var cut = RenderDialog(open: true);

        cut.Find("button[autofocus]").Click();

        cancelled.ShouldBe(1);
        confirmed.ShouldBe(0);
    }

    [Fact]
    public void BrowserClosingIt_WhileOpen_CountsAsCancel()
    {
        var cut = RenderDialog(open: true);

        cut.Find("dialog").TriggerEvent("onclose", EventArgs.Empty);   // Escape

        cancelled.ShouldBe(1);
    }

    [Fact]
    public void ClosingAfterThePageClosedIt_IsNotASecondCancel()
    {
        var cut = RenderDialog(open: true);
        cut.Render(p => p.Add(d => d.Open, false));
        JSInterop.VerifyInvoke("appDialog.close");

        cut.Find("dialog").TriggerEvent("onclose", EventArgs.Empty);

        cancelled.ShouldBe(0);
    }
}
