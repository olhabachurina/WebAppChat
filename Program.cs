using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WebAppChat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebAppChat.Data;
using WebAppChat.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/";
    options.SlidingExpiration = true;
});


builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapPost("/register", async (HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) =>
{
    string username = context.Request.Form["username"];
    string password = context.Request.Form["password"];

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
    {
        return Results.BadRequest("Username and password must be provided.");
    }

    var user = new ApplicationUser { UserName = username, CreatedAt = DateTime.UtcNow };
    var result = await userManager.CreateAsync(user, password);

    if (result.Succeeded)
    {
        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok();
    }

    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
    return Results.BadRequest(new { Message = "Registration failed", Errors = errors });
});


app.MapPost("/login", async (HttpContext context, SignInManager<ApplicationUser> signInManager) =>
{
    string username = context.Request.Form["username"];
    string password = context.Request.Form["password"];

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
    {
        return Results.BadRequest("Username and password must be provided.");
    }

    var result = await signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        return Results.Ok();
    }

    return Results.BadRequest("Login failed. Please check your credentials and try again.");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<ChatHub>("/chathub").RequireAuthorization();
    endpoints.MapFallbackToFile("index.html");
});

app.Run();