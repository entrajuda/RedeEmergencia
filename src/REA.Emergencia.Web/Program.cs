using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Models;
using REA.Emergencia.Web.Options;
using REA.Emergencia.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Este campo é obrigatório.");
    options.ModelBindingMessageProvider.SetMissingBindRequiredValueAccessor(fieldName => $"O campo '{fieldName}' é obrigatório.");
    options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(() => "É necessário um valor.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, fieldName) => $"O valor '{value}' não é válido para {fieldName}.");
    options.ModelBindingMessageProvider.SetUnknownValueIsInvalidAccessor(fieldName => $"O valor fornecido não é válido para {fieldName}.");
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(value => $"O valor '{value}' não é válido.");
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<PedidoBemInputModelValidator>();

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BackofficeAdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });
});

builder.Services.Configure<AzureAdRoleManagementOptions>(builder.Configuration.GetSection("AzureAdRoleManagement"));
builder.Services.Configure<GraphMailOptions>(builder.Configuration.GetSection("GraphMail"));

builder.Services.AddScoped<IAzureAdRoleManagementService, AzureAdRoleManagementService>();
builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.AddScoped<IRequestNotificationEmailService, RequestNotificationEmailService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=PedidosBens}/{action=Index}/{id?}");

app.Run();
