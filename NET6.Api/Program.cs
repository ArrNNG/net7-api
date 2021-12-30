using Autofac;
using Autofac.Extensions.DependencyInjection;
using CSRedis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NET6.Api.Services;
using NET6.Domain.Enums;
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

#region ע�����ݿ��Redis
builder.Services.AddScoped(options =>
{
    return new SqlSugarClient(new List<ConnectionConfig>()
    {
        new ConnectionConfig() { ConfigId = DBEnum.Ĭ�����ݿ�, ConnectionString = _config.GetConnectionString("SugarConnectString"), DbType = DbType.MySql, IsAutoCloseConnection = true }
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

#region ��ʼ����־
Log.Logger = new LoggerConfiguration()
       .MinimumLevel.Error()
       .WriteTo.File(Path.Combine("Logs", @"Log.txt"), rollingInterval: RollingInterval.Day)
       .CreateLogger();
#endregion

#region ���������ͬ��IO
builder.Services.Configure<KestrelServerOptions>(x => x.AllowSynchronousIO = true)
        .Configure<IISServerOptions>(x => x.AllowSynchronousIO = true);
#endregion

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

#region ע���̨����
builder.Services.AddHostedService<TimerService>();
#endregion

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
var path = Path.Combine(basePath, "Files/");
CommonFun.CreateDir(path);
//MIME֧��
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".fbx"] = "application/octet-stream";
provider.Mappings[".obj"] = "application/octet-stream";
provider.Mappings[".mtl"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(path),
    ContentTypeProvider = provider,
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
