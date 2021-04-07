/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * SPDX-License-Identifier: MIT-0
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this
 * software and associated documentation files (the "Software"), to deal in the Software
 * without restriction, including without limitation the rights to use, copy, modify,
 * merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace AWS.Samples.Amazon.Cognito.Demo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ResponseType = Configuration["Authentication:Cognito:ResponseType"];
                options.MetadataAddress = Configuration["Authentication:Cognito:MetadataAddress"];
                options.ClientId = Configuration["Authentication:Cognito:ClientId"];
                options.Events = new OpenIdConnectEvents()
                {
                    OnRedirectToIdentityProviderForSignOut = OnRedirectToIdentityProviderForSignOut
                };

                //enable this code block if you want to leverage Role Based Authorization
                //options.TokenValidationParameters = new TokenValidationParameters
                //{
                //    ValidateIssuer = options.TokenValidationParameters.ValidateIssuer,
                //    RoleClaimType = "cognito:groups"
                //};
            });

            //enable this code block if you want to leverage Policy Based Authorization
            //we use Amazon Cognito users attributes to support claim-based authorization.
            /* You can use [Authorize] to ensure that only logged-in users can access the Page/Controller/Route.
             * However you?d probably like more fine-grained control, so for that you can add users to Cognito Groups. On the ASP.NET Core app,
             * those groups are sent as part of the user Claims. You can create authorization polices during the Startup.Configure method as follows:
             */
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c => c.Type == "cognito:groups" && c.Value == "Admin")));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
            name: "products",
            pattern: "products/{action}/{id?}",
            defaults: new { controller = "Products", action = "Index" });

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private Task OnRedirectToIdentityProviderForSignOut(RedirectContext context)
        {
            context.ProtocolMessage.Scope = "openid";
            context.ProtocolMessage.ResponseType = "code";

            var cognitoDomain = Configuration["Authentication:Cognito:CognitoDomain"];

            var clientId = Configuration["Authentication:Cognito:ClientId"];

            var logoutUrl = $"{context.Request.Scheme}://{context.Request.Host}{Configuration["Authentication:Cognito:AppSignOutUrl"]}";

            context.ProtocolMessage.IssuerAddress = $"{cognitoDomain}/logout?client_id={clientId}&logout_uri={logoutUrl}&redirect_uri={logoutUrl}";

            // delete cookies
            context.Properties.Items.Remove(CookieAuthenticationDefaults.AuthenticationScheme);
            // close openid session
            context.Properties.Items.Remove(OpenIdConnectDefaults.AuthenticationScheme);

            return Task.CompletedTask;
        }
    }
}
