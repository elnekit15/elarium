using Microsoft.AspNetCore.Mvc;

namespace DesignerStore.Controllers;

public class InfoController : Controller
{
    public IActionResult About()    => View();
    public IActionResult Contacts() => View();
    public IActionResult FAQ()      => View();
    public IActionResult Delivery() => View();
}
