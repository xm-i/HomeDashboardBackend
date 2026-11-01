
using System;
using System.Collections.Generic;
using System.Linq;

using Back.Hubs;
using Back.Models.Common;
using Back.Models.ElectricPower;
using Back.Models.Financial;
using Back.Models.HealthCheck;
using Back.Models.Kitchen;
using Back.Models.Network;
using Back.Models.Palmie;

using DataBase;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Back; 
public class Startup {
	private readonly string _crossOriginPolicyName = "anyOrigin";
	public IConfiguration Configuration {
		get;
	}


	public Startup(IWebHostEnvironment env) {
		var builder = new ConfigurationBuilder()
			.SetBasePath(env.ContentRootPath)
			.AddJsonFile("appsettings.json", false, true)
			.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
			.AddEnvironmentVariables();
		this.Configuration = builder.Build();

		Console.WriteLine("設定値");
		foreach (var section in this.Configuration.GetChildren()) {
			Console.WriteLine($"{section.Path}:{section.Value}");
		}
	}

	private string[] GetAllowedOrigins() {
		return this.Configuration.GetSection("FrontOrigin").Get<string[]>()
			?? (string.IsNullOrWhiteSpace(this.Configuration.GetSection("FrontOrigin").Value)
				? []
				: [this.Configuration.GetSection("FrontOrigin").Value!]);
	}

	// This method gets called by the runtime. Use this method to add services to the container.
	// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
	public void ConfigureServices(IServiceCollection services) {
		var allowedOrigins = this.GetAllowedOrigins();
		Console.WriteLine($"許可Origin一覧: {string.Join(", ", allowedOrigins)}");

		// クロスドメインの許可
		services.AddCors(options => {
			options.AddPolicy(this._crossOriginPolicyName, builder => {
				builder.AllowCredentials();
				if (allowedOrigins.Length > 0) {
					builder.WithOrigins(allowedOrigins);
				}
				builder.AllowAnyHeader();
				builder.AllowAnyMethod();
			});
		});

		services.AddMvc();
		services.AddSignalR();
		services.AddDbContext<HomeServerDbContext>(optionsBuilder => {
			var connectionString = this.Configuration.GetConnectionString("Database");
			optionsBuilder.UseNpgsql(connectionString);
		});
		services.AddLogging(builder => {
			builder.AddConfiguration(this.Configuration.GetSection("Logging"))
				.AddConsole()
				.AddDebug();
		});
		services.AddHttpClient();

		services.AddSingleton<Updater>();
		services.AddTransient<FinancialModel>();
		services.AddTransient<ElectricPowerModel>();
		services.AddTransient<NetworkModel>();
		services.AddTransient<KitchenModel>();
		services.AddTransient<PalmieModel>();
		services.AddTransient<HealthCheckModel>();
	}

	// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
		if (env.IsDevelopment()) {
			app.UseDeveloperExceptionPage();
		} else {
			app.UseHsts();
		}

		// リバースプロキシ配下での HTTPS / ホスト名 / IP 認識用
		app.UseForwardedHeaders(new ForwardedHeadersOptions {
			ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
		});

		app.UseRouting();

		app.UseCors(this._crossOriginPolicyName);

		app.UseEndpoints(routes => {
			routes.MapHub<DashboardHub>("api/hubs/dashboard-hub");
		});

		app.UseEndpoints(routes => {
			routes.MapControllers();
		});

	}
}
