using Bunit;
using RecipesApp.Pages;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class ImportExportPageTests : ComponentTestBase
{
    private const string TwoRecipes = """
        ===== RECIPE =====
        Name: Toast
        Cooking Time: 5
        Servings: 1
        Tags: Breakfast,Quick

        ===== INGREDIENTS =====
        1 pieces bread

        ===== INSTRUCTIONS =====
        Toast it.

        ===== END RECIPE =====

        ===== RECIPE =====
        Name: Tea
        Cooking Time: 3 minutes
        Servings: 2

        ===== INGREDIENTS =====
        2 pieces tea bags
        500ml water

        ===== INSTRUCTIONS =====
        Brew it.

        ===== END RECIPE =====
        """;

    public ImportExportPageTests()
    {
        JSInterop.SetupVoid("downloadFile", _ => true).SetVoidResult();
    }

    private static void Click(IRenderedComponent<ImportExport> cut, string label) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == label).Click();

    private IRenderedComponent<ImportExport> RenderWithPastedText(string text)
    {
        var cut = Render<ImportExport>();
        cut.Find("#import-text").Change(text);
        Click(cut, "Read text");
        return cut;
    }

    [Fact]
    public void ReadingText_ShowsAPreviewList_NotATable()
    {
        var cut = RenderWithPastedText(TwoRecipes);

        cut.FindAll("table").ShouldBeEmpty();
        cut.FindAll(".import-preview-name").Select(n => n.TextContent).ShouldBe(new[] { "Toast", "Tea" });
        var meta = cut.FindAll(".import-preview-meta").Select(m => m.TextContent).ToArray();
        meta[0].ShouldBe("5 min · 1 serving · 1 ingredient · Breakfast, Quick");
        meta[1].ShouldBe("3 min · 2 servings · 2 ingredients");
        cut.Find(".action-bar-status").TextContent.ShouldBe("2 recipes");
    }

    [Fact]
    public void Import_SavesTheRecipes_AndSaysHowMany()
    {
        var cut = RenderWithPastedText(TwoRecipes);

        Click(cut, "Import");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Successfully imported 2 recipes."));
        cut.FindAll(".import-preview").ShouldBeEmpty();
        using var context = Db.CreateContext();
        context.Recipes.Select(r => r.Name).OrderBy(n => n).ToArray().ShouldBe(new[] { "Tea", "Toast" });
    }

    [Fact]
    public void Cancel_ClearsThePreview_WithoutImporting()
    {
        var cut = RenderWithPastedText(TwoRecipes);

        Click(cut, "Cancel");

        cut.FindAll(".import-preview").ShouldBeEmpty();
        using var context = Db.CreateContext();
        context.Recipes.Count().ShouldBe(0);
    }

    [Fact]
    public void BadText_ShowsWarnings_InTheAnnouncedRegion()
    {
        var cut = RenderWithPastedText("not a recipe at all");

        var region = cut.Find("#import-heading").ParentElement!.QuerySelector("[aria-live=polite]")!;
        region.QuerySelector(".alert-warning").ShouldNotBeNull();
        cut.FindAll(".action-bar").ShouldBeEmpty();
    }

    [Fact]
    public void ReadText_IsDisabledUntilThereIsText()
    {
        var cut = Render<ImportExport>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Read text").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void ChoosingAFile_ReadsItToo()
    {
        var cut = Render<ImportExport>();

        cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>()
            .UploadFiles(InputFileContent.CreateFromText(TwoRecipes, "recipes.txt"));

        cut.WaitForAssertion(() => cut.FindAll(".import-preview li").Count.ShouldBe(2));
    }

    [Fact]
    public void Export_DownloadsAFile_AndSaysHowMany()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<ImportExport>();

        Click(cut, "Export all recipes");

        cut.Find("#export-heading").ParentElement!.TextContent.ShouldContain("Exported 1 recipe to recipes_export_");
        JSInterop.VerifyInvoke("downloadFile");
    }

    [Fact]
    public void EveryField_HasALabel()
    {
        var cut = Render<ImportExport>();

        foreach (var field in cut.FindAll("input, textarea"))
        {
            var id = field.GetAttribute("id");
            id.ShouldNotBeNullOrEmpty($"a {field.TagName} has no id");
            cut.FindAll($"label[for='{id}']").Count.ShouldBe(1, $"#{id} has no label");
        }
    }

    [Fact]
    public void FormatGuide_IsAClosedDisclosure_WithHeadingsInOrder()
    {
        var cut = Render<ImportExport>();

        cut.Find("details summary").TextContent.ShouldBe("Format guide");
        cut.Find("details").HasAttribute("open").ShouldBeFalse();
        cut.FindAll("h4, h5, h6").ShouldBeEmpty();   // no skipped heading levels
    }
}
