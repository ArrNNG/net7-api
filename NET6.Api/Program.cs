var builder = WebApplication.CreateBuilder(args);
var basePath = AppContext.BaseDirectory;

//���������ļ�
var _config = new ConfigurationBuilder()
                 .SetBasePath(basePath)
                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                 .Build();

#region �ӿڷ���
var groups = new List<Tuple<string, string>>
{
    //new Tuple<string, string>("Group1","����һ"),
    //new Tuple<string, string>("Group2","�����")
};
#endregion

#region ע�����ݿ�
var dbtype = DbType.SqlServer;
if (_config.GetConnectionString("SugarConnectDBType") == "mysql")
{
    dbtype = DbType.MySql;
}
builder.Services.AddSingleton(options =>
{
    return new SqlSugarScope(new List<ConnectionConfig>()
    {
        new ConnectionConfig() { ConfigId = DBEnum.Ĭ�����ݿ�, ConnectionString = _config.GetConnectionString("SugarConnectString"), DbType = dbtype, IsAutoCloseConnection = true }
    });
});
#endregion

#region ��ʼ��Redis
RedisHelper.Initialization(new CSRedisClient(_config.GetConnectionString("CSRedisConnectString")));
#endregion

#region ���swaggerע��
if (_config["UseSwagger"].ToBool())
{
    builder.Services.AddSwaggerGen(a =>
    {
        a.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Api",
            Description = "Api�ӿ��ĵ�"
        });
        foreach (var item in groups)
        {
            a.SwaggerDoc(item.Item1, new OpenApiInfo { Version = item.Item1, Title = item.Item2, Description = $"{item.Item2}�ӿ��ĵ�" });
        }
        a.IncludeXmlComments(Path.Combine(basePath, "NET6.Api.xml"), true);
        a.IncludeXmlComments(Path.Combine(basePath, "NET6.Domain.xml"), true);
        a.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "Value: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        a.AddSecurityRequirement(new OpenApiSecurityRequirement()
        {{
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
}
#endregion

#region ��������֤
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
        ClockSkew = TimeSpan.Zero
    };
    //����JWT�����¼�
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("jwtexception", "expired");
            }
            return Task.CompletedTask;
        }
    };
});
#endregion

#region ��ʼ����־
builder.Host.UseSerilog((builderContext, config) =>
{
    config
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine("Logs", @"Log.txt"), rollingInterval: RollingInterval.Day);
});
#endregion

#region ���������ͬ��IO
builder.Services.Configure<KestrelServerOptions>(a => a.AllowSynchronousIO = true)
                .Configure<IISServerOptions>(a => a.AllowSynchronousIO = true);
#endregion

#region ��ʼ��Autofac ע�����
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
var hostBuilder = builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
{
    var assembly = Assembly.Load("NET6.Infrastructure");
    builder.RegisterAssemblyTypes(assembly).Where(a => a.Name.EndsWith("Repository")).AsSelf();
});
#endregion

#region ��ʼ��AutoMapper �Զ�ӳ��
var serviceAssembly = Assembly.Load("NET6.Domain");
builder.Services.AddAutoMapper(serviceAssembly);
#endregion

#region ע���̨����
builder.Services.AddHostedService<TimerService>();
#endregion

#region ע���¼�����
builder.Services.AddEventBus(builder =>
{
    builder.ChannelCapacity = 5000;
    builder.AddSubscriber<LoginSubscriber>();
    builder.UnobservedTaskExceptionHandler = (obj, args) =>
    {
        Log.Error($"�¼������쳣��{args.Exception}");
    };
    //builder.ReplaceStorer(serviceProvider =>
    //{
    //    return new RedisEventSourceStorer();
    //});
});
#endregion

#region ע��ϵͳ����
builder.Services.AddMemoryCache();
#endregion

#region ע��http������
builder.AddServiceProvider();
#endregion

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LogActionFilter>();
    options.Filters.Add<GlobalExceptionFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(new DatetimeJsonConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Configure the HTTP request pipeline.

#region ���þ�̬��Դ����
//����Ŀ¼
var path = Path.Combine(basePath, "Files/");
CommonFun.CreateDir(path);
//���MIME֧��
var provider = new FileExtensionContentTypeProvider();
provider.Mappings.Add(".fbx", "application/octet-stream");
provider.Mappings.Add(".obj", "application/octet-stream");
provider.Mappings.Add(".mtl", "application/octet-stream");
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
if (_config["UseSwagger"].ToBool())
{
    app.UseSwagger();
    app.UseSwaggerUI(a =>
    {
        a.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
        foreach (var item in groups)
        {
            a.SwaggerEndpoint($"/swagger/{item.Item1}/swagger.json", item.Item2);
        }
        a.RoutePrefix = string.Empty;
        a.DocExpansion(DocExpansion.None);
        a.DefaultModelsExpandDepth(-1);//����ʾModels
    });
}
#endregion

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
