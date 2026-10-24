using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using SaPosts.Web.Data;
using SaPosts.Web.Media;

namespace SaPosts.Web.Pages.Admin.Media;

public sealed record ImageOption(Guid Id, string Label);

public sealed class MediaForm
{
    [Display(Name = "Texte alternatif")]
    [StringLength(500, ErrorMessage = "500 caractères maximum.")]
    public string? AltText { get; set; }

    [Display(Name = "Image d’aperçu (affichée avant la lecture)")]
    public Guid? PosterId { get; set; }
}

public sealed class EditModel(BlogDbContext db, MediaLibrary library) : PageModel
{
    [BindProperty]
    public MediaForm Form { get; set; } = new();

    public MediaItem Item { get; private set; } = null!;

    public IReadOnlyList<string> Uses { get; private set; } = [];

    public IReadOnlyList<ImageOption> PosterOptions { get; private set; } = [];

    public bool Saved { get; private set; }

    public string Markdown => $"![{Item.AltText}]({MediaFiles.Reference(Item.Id)})";

    public async Task<IActionResult> OnGetAsync(Guid id, bool saved, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        Saved = saved;
        Form = new MediaForm { AltText = Item.AltText, PosterId = Item.PosterId };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            switch (await library.UpdateAsync(id, Form.AltText, Form.PosterId, cancellationToken))
            {
                case MediaUpdateOutcome.Saved:
                    return RedirectToPage(new { id, saved = true });
                case MediaUpdateOutcome.NotFound:
                    return NotFound();
                case MediaUpdateOutcome.AltTextRequired:
                    ModelState.AddModelError($"{nameof(Form)}.{nameof(MediaForm.AltText)}", "Le texte alternatif est obligatoire pour une image.");
                    break;
                case MediaUpdateOutcome.InvalidPoster:
                    ModelState.AddModelError($"{nameof(Form)}.{nameof(MediaForm.PosterId)}", "Choisissez une image de la médiathèque.");
                    break;
            }
        }

        return await LoadAsync(id, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await library.DeleteAsync(id, cancellationToken))
        {
            return RedirectToPage("/Admin/Media/Index", new { deleted = true });
        }

        // Still in use (or already gone): show the page again with the list of uses.
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        Form = new MediaForm { AltText = Item.AltText, PosterId = Item.PosterId };
        return Page();
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Media.AsNoTracking().Include(m => m.Poster).SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        Item = item;
        Uses = await library.FindUsesAsync(id, cancellationToken);
        if (item.Kind == MediaKind.Video)
        {
            PosterOptions = await db.Media.AsNoTracking()
                .Where(m => m.Kind == MediaKind.Image)
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => new ImageOption(m.Id, m.AltText ?? m.OriginalFileName))
                .ToListAsync(cancellationToken);
        }

        return true;
    }
}
