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

//�����������������
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

//���ý���Form��󳤶�
builder.Services.Configure<FormOptions>(option => {
    option.MultipartBodyLengthLimit = int.MaxValue;
});

//����Serilog
var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
builder.Host.UseSerilog();

//��ӽ������
builder.Services.AddHealthChecks();

//����Consul
builder.Services.AddConsul(options => options.Address = new Uri(builder.Configuration["Consul:Address"]!));
builder.Services.AddConsulServiceRegistration(options =>
{
    options.Check = new AgentServiceCheck()
    {
        DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5), //����ֹͣ���к�೤ʱ���Զ�ע���÷���
        Interval = TimeSpan.FromSeconds(60), //���������
        HTTP = "http://" + builder.Configuration["Consul:IP"]! + ":" + builder.Configuration["Consul:Port"]! + "/health", //��������ַ
        Timeout = TimeSpan.FromSeconds(10), //��ʱʱ��
    };
    options.ID = builder.Configuration["Consul:ID"]!;
    options.Name = builder.Configuration["Consul:Name"]!;
    options.Address = builder.Configuration["Consul:IP"]!;
    options.Port = int.Parse(builder.Configuration["Consul:Port"]!);
});

//����DataCollection
builder.Services.Configure<LeafletCollectionSettings>(
    builder.Configuration.GetSection("LeafletCollection"));

builder.Services.AddSingleton<LeafletService>();

builder.Services.Configure<UserCardCollectionSettings>(
    builder.Configuration.GetSection("UserCardCollection"));

builder.Services.AddSingleton<UserCardService>();

//����Redis
builder.Services.AddSingleton<RedisConnection>();

//����MinIO
builder.Services.AddMinio(options =>
{
    options.Endpoint = builder.Configuration["MinIO:Endpoint"]!;
    options.AccessKey = builder.Configuration["MinIO:AccessKey"]!;
    options.SecretKey = builder.Configuration["MinIO:SecretKey"]!;
});
builder.Services.AddSingleton<LeafletMediasMinIOService>();
builder.Services.AddSingleton<UserCardMinIOService>();

//���÷�����
builder.Services.AddSingleton<IServiceDiscover, ServiceDiscover>();

//����gRPC
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

//����Filters
builder.Services.AddScoped<JWTAuthFilterService>();

var app = builder.Build();

//ʹ��Serilog����������־
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//���ý���״̬����м��
app.UseHealthChecks("/health");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
