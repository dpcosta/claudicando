using Web.Blazor.Components;
using Web.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add ServiceDefaults (deve ser o primeiro)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClient para CatalogService com Service Discovery
builder.Services.AddHttpClient<ICatalogService, CatalogService>(client =>
{
    // O Service Discovery do Aspire vai resolver automaticamente via http://catalog-api
    client.BaseAddress = new Uri("http://catalog-api");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForErrors: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
