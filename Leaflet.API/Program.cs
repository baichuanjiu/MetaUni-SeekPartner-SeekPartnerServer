using Consul;
using Consul.AspNetCore;
using Leaflet.API.DataCollection.Leaflet;
using Leaflet.API.DataCollection.UserCard;
using Leaflet.API.Filters;
using Leaflet.API.MinIO;
using Leaflet.API.MongoDBServices.Leaflet;
using Leaflet.API.MongoDBServices.UserCard;
using Leaflet.API.Protos.Authentication;
using Leaflet.API.Protos.BriefUserInfo;
using Leaflet.API.Protos.ChatRequest;
using Leaflet.API.Redis;
using Leaflet.API.ServiceDiscover;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Minio.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//配置请求体最大容量
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

//配置接收Form最大长度
builder.Services.Configure<FormOptions>(option => {
    option.MultipartBodyLengthLimit = int.MaxValue;
});

//配置Serilog
var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
builder.Host.UseSerilog();

//添加健康检查
builder.Services.AddHealthChecks();

//配置Consul
builder.Services.AddConsul(options => options.Address = new Uri(builder.Configuration["Consul:Address"]!));
builder.Services.AddConsulServiceRegistration(options =>
{
    options.Check = new AgentServiceCheck()
    {
        DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5), //服务停止运行后多长时间自动注销该服务
        Interval = TimeSpan.FromSeconds(60), //心跳检查间隔
        HTTP = "http://" + builder.Configuration["Consul:IP"]! + ":" + builder.Configuration["Consul:Port"]! + "/health", //健康检查地址
        Timeout = TimeSpan.FromSeconds(10), //超时时间
    };
    options.ID = builder.Configuration["Consul:ID"]!;
    options.Name = builder.Configuration["Consul:Name"]!;
    options.Address = builder.Configuration["Consul:IP"]!;
    options.Port = int.Parse(builder.Configuration["Consul:Port"]!);
});

//配置DataCollection
builder.Services.Configure<LeafletCollectionSettings>(
    builder.Configuration.GetSection("LeafletCollection"));

builder.Services.AddSingleton<LeafletService>();

builder.Services.Configure<UserCardCollectionSettings>(
    builder.Configuration.GetSection("UserCardCollection"));

builder.Services.AddSingleton<UserCardService>();

//配置Redis
builder.Services.AddSingleton<RedisConnection>();

//配置MinIO
builder.Services.AddMinio(options =>
{
    options.Endpoint = builder.Configuration["MinIO:Endpoint"]!;
    options.AccessKey = builder.Configuration["MinIO:AccessKey"]!;
    options.SecretKey = builder.Configuration["MinIO:SecretKey"]!;
});
builder.Services.AddSingleton<LeafletMediasMinIOService>();
builder.Services.AddSingleton<UserCardMinIOService>();

//配置服务发现
builder.Services.AddSingleton<IServiceDiscover, ServiceDiscover>();

//配置gRPC
builder.Services
    .AddGrpcClient<Authenticate.AuthenticateClient>((serviceProvider, options) =>
    {
        var discover = serviceProvider.GetRequiredService<IServiceDiscover>();
        string address = discover.GetService(builder.Configuration["ServiceDiscover:ServiceName:Auth"]!);
        options.Address = new Uri(address);
    })
    .AddCallCredentials((context, metadata) =>
    {
        metadata.Add("id", builder.Configuration["RPCHeader:ID"]!);
        metadata.Add("jwt", builder.Configuration["RPCHeader:JWT"]!);
        return Task.CompletedTask;
    })
    .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
    ;

builder.Services
    .AddGrpcClient<GetBriefUserInfo.GetBriefUserInfoClient>((serviceProvider, options) =>
    {
        var discover = serviceProvider.GetRequiredService<IServiceDiscover>();
        string address = discover.GetService(builder.Configuration["ServiceDiscover:ServiceName:User"]!);
        options.Address = new Uri(address);
    })
    .AddCallCredentials((context, metadata) =>
    {
        metadata.Add("id", builder.Configuration["RPCHeader:ID"]!);
        metadata.Add("jwt", builder.Configuration["RPCHeader:JWT"]!);
        return Task.CompletedTask;
    })
    .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
    ;

builder.Services
    .AddGrpcClient<SendChatRequest.SendChatRequestClient>((serviceProvider, options) =>
    {
        var discover = serviceProvider.GetRequiredService<IServiceDiscover>();
        string address = discover.GetService(builder.Configuration["ServiceDiscover:ServiceName:Message"]!);
        options.Address = new Uri(address);
    })
    .AddCallCredentials((context, metadata) =>
    {
        metadata.Add("id", builder.Configuration["RPCHeader:ID"]!);
        metadata.Add("jwt", builder.Configuration["RPCHeader:JWT"]!);
        return Task.CompletedTask;
    })
    .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
    ;

//配置Filters
builder.Services.AddScoped<JWTAuthFilterService>();

var app = builder.Build();

//使用Serilog处理请求日志
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//启用健康状态检查中间件
app.UseHealthChecks("/health");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
