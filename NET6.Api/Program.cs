using Autofac;
using Autofac.Extensions.DependencyInjection;
using CSRedis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NET6.Api.Service;
using NET6.Infrastructure.Tools;
using Serilog;
using SqlSugar;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var basePath = AppContext.BaseDirectory;

//���������ļ�
var _config = new ConfigurationBuilder()
                 .SetBasePath(basePath)
                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                 .Build();

#region ע�����ݿ��redis
builder.Services.AddScoped(options =>
{
    return new SqlSugarClient(new ConnectionConfig()
    {
        ConnectionString = _config.GetConnectionString("SugarConnectString"),
        DbType = DbType.MySql,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
});
RedisHelper.Initialization(new CSRedisClient(_config.GetConnectionString("CSRedisConnectString")));
#endregion


#region ���swaggerע��
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Api"
    });
    var xmlPath = Path.Combine(basePath, "NET6.Api.xml");
    c.IncludeXmlComments(xmlPath, true);
    var xmlDomainPath = Path.Combine(basePath, "NET6.Domain.xml");
    c.IncludeXmlComments(xmlDomainPath, true);
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Value: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
      {
        new OpenApiSecurityScheme
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
          },Scheme = "oauth2",Name = "Bearer",In = ParameterLocation.Header,
        },new List<string>()
      }
    });
});
#endregion


#region ���У��
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = "net6api.com",
        ValidIssuer = "net6api.com",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSecurityKey"])),
    };
});
#endregion

Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Error()
               .WriteTo.File(Path.Combine("Logs", @"Log.txt"), rollingInterval: RollingInterval.Day)
               .CreateLogger();

builder.Services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true)
                .Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);

#region ����ע��Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
var hostBuilder = builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
{
    try
    {
        var assemblyServices = Assembly.Load("NET6.Infrastructure");
        builder.RegisterAssemblyTypes(assemblyServices).Where(a => a.Name.EndsWith("Repository")).AsSelf();
    }
    catch (Exception ex)
    {
        throw new Exception(ex.Message + "\n" + ex.InnerException);
    }
});
#endregion

//ע�����
builder.Services.AddHostedService<TimerServicce>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

#region ���þ�̬��Դ����
//����Ŀ¼
CommonFun.CreateDir(Path.Combine(basePath, "Files/"));
//app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(basePath, "Files/")),
    RequestPath = "/Files"
});
#endregion


#region ���ÿ������
app.UseCors(builder => builder
       .WithOrigins(_config["Origins"])
       .AllowCredentials()
       .AllowAnyMethod()
       .AllowAnyHeader());
#endregion


app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


#region ����swaggerUI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
    c.RoutePrefix = string.Empty;
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.DefaultModelsExpandDepth(-1);
});

#endregion


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
