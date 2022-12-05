using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var cosmosConnectionString = builder.Configuration["COSMOS_CONNECTION_STRING"];

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddAuthentication(options => 
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options => 
{
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
})
.AddGitHub(options => 
{
    options.ClientId = builder.Configuration["Github:ClientId"];
    options.ClientSecret = builder.Configuration["Github:ClientSecret"];
    options.Scope.Add("user:email");
    options.Scope.Add("repo");
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
