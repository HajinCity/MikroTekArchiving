using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MikroTekArchiving.Models;

namespace MikroTekArchiving.Controllers;

public class HomeController : Controller
{
    // The site's home page is the hotspot login page.
    public IActionResult Index() => RedirectToAction("Login", "Hotspot");

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // Tell users plainly when the problem is the database (SQL Server stopped / wrong connection string).
        var ex = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        var dbDown = false;
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException) { dbDown = true; break; }

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            DatabaseDown = dbDown
        });
    }
}
