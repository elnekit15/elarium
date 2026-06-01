using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DesignerStore.Controllers;

public class DefileController : Controller
{
    private readonly IWebHostEnvironment _env;
    private const string VideoFileName = "defile_main.mp4";
    private const string DescFileName  = "defile_description.txt";

    public DefileController(IWebHostEnvironment env)
    {
        _env = env;
    }

    // ---------- helpers ----------

    private string DescriptionPath => Path.Combine(_env.WebRootPath, "images", DescFileName);

    private string ReadDescription()
    {
        var path = DescriptionPath;
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Trim() : string.Empty;
    }

    private void WriteDescription(string? text)
    {
        System.IO.File.WriteAllText(DescriptionPath, text?.Trim() ?? string.Empty);
    }

    // ---------- actions ----------

    public IActionResult Index()
    {
        var videoPath = Path.Combine(_env.WebRootPath, "images", VideoFileName);
        ViewBag.HasVideo    = System.IO.File.Exists(videoPath);
        ViewBag.Description = ReadDescription();
        return View();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Manage()
    {
        var videoPath = Path.Combine(_env.WebRootPath, "images", VideoFileName);
        ViewBag.HasVideo    = System.IO.File.Exists(videoPath);
        ViewBag.Description = ReadDescription();
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Upload(IFormFile? video, string? description)
    {
        WriteDescription(description);

        if (video != null && video.Length > 0)
        {
            var ext = Path.GetExtension(video.FileName).ToLower();
            if (ext != ".mp4")
            {
                TempData["Error"] = "Дозволено лише .mp4 файли.";
                return RedirectToAction("Manage");
            }

            var uploadPath = Path.Combine(_env.WebRootPath, "images", VideoFileName);
            await using var stream = new FileStream(uploadPath, FileMode.Create);
            await video.CopyToAsync(stream);

            TempData["Success"] = "Відео та опис колекції збережено!";
        }
        else
        {
            TempData["Success"] = "Опис колекції збережено!";
        }

        return RedirectToAction("Manage");
    }
}
