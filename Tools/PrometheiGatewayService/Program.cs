using ArgsUniform;
using Logging;

namespace PrometheiGatewayService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var uniformArgs = new ArgsUniform<Configuration>(PrintHelp, args);
            var config = uniformArgs.Parse(true);

            var log = new TimestampPrefixer(
                new LogSplitter(
                    new FileLog(Path.Combine(config.DataPath, "gateway")),
                    new ConsoleLog()
                )
            );

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSingleton<ILog>(log);
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton<NodeSelector>();
            builder.Services.AddSingleton<AppMetrics>();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            app.Services.GetService<NodeSelector>()!.Initialize().Wait();
            app.Services.GetService<AppMetrics>()!.Initialize();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run(config.ListenUrl);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Promethei Gateway Service");
        }
    }
}
