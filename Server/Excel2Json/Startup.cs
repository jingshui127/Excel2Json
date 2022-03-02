using Excel2Json.Data;
using Excel2Json.Options;
using Excel2Json.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Excel2Json
{
    public class Startup
    {
        readonly string DebugCorsPolicy = "DebugCorsPolicy";
        public const string ShareCorsPolicy = "ShareCorsPolicy";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().Options;
            using (var db = new ApplicationDbContext(options))
            {
                //db.Database.EnsureDeleted(); //clean up for testing
                db.Database.EnsureCreated();
                db.Database.Migrate();
            }
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>();

            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
            }).AddEntityFrameworkStores<ApplicationDbContext>();

            services.Configure<JwtOptions>(Configuration.GetSection(JwtOptions.Jwt));
            services.Configure<GoogleOptions>(Configuration.GetSection(GoogleOptions.Google));

            var jwtOptions = Configuration.GetSection(JwtOptions.Jwt).Get<JwtOptions>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience
                };
            });

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<ITokenService, TokenService>();

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.EnableForHttps = true;
                options.MimeTypes = new[]
                {
                    // Default
                    "text/plain",
                    "text/css",
                    "application/javascript",
                    "text/html",
                    "application/xml",
                    "text/xml",
                    "application/json",
                    "text/json",

                    // Custom
                    "image/svg+xml",
                    "application/font-woff2"
                };
            });

            services.AddSpaStaticFiles(options =>
            {
                options.RootPath = "wwwroot";
            });

            services.AddControllers();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Excel2Json", Version = "v1" });
            });

            services.AddCors(options =>
            {
                options.AddPolicy(DebugCorsPolicy, builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .WithExposedHeaders("location");
                });
                options.AddPolicy(ShareCorsPolicy, builder =>
                {
                    builder.AllowAnyOrigin().WithMethods("Get");
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Excel2Json v1"));
            }
            else
            {
                app.UseForwardedHeaders();
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseDefaultFiles();

            app.UseResponseCompression();
            app.UseSpaStaticFiles();

            app.UseRouting();

            if (env.IsDevelopment())
                app.UseCors(DebugCorsPolicy);

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSpa(spa =>
            {
                spa.Options.DefaultPage = "/index.html";
            });
        }
    }
}
