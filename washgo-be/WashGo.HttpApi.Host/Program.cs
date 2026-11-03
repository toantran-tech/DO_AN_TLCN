using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WashGo.HttpApi.Host
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            LoadDotEnv();

            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseAutofac();

            await builder.AddApplicationAsync<WashGoHttpApiHostModule>();

            var app = builder.Build();

            await app.InitializeApplicationAsync();
            await app.RunAsync();
        }

        private static void LoadDotEnv()
        {
            try
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null)
                {
                    var envPath = Path.Combine(directory.FullName, ".env");
                    if (File.Exists(envPath))
                    {
                        foreach (var rawLine in File.ReadAllLines(envPath))
                        {
                            var line = rawLine.Trim();
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                            var separatorIndex = line.IndexOf('=');
                            if (separatorIndex <= 0) continue;

                            var key = line.Substring(0, separatorIndex).Trim();
                            var value = line.Substring(separatorIndex + 1).Trim().Trim('"', '\'');

                            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                            {
                                Environment.SetEnvironmentVariable(key, value);
                            }

                            // Ánh xạ tiện lợi sang định dạng IConfiguration của .NET (dùng __ thay cho :)
                            if (key == "SMTP_HOST" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__Host")))
                                Environment.SetEnvironmentVariable("Email__Smtp__Host", value);
                            if (key == "SMTP_PORT" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__Port")))
                                Environment.SetEnvironmentVariable("Email__Smtp__Port", value);
                            if (key == "SMTP_ENABLE_SSL" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__EnableSsl")))
                                Environment.SetEnvironmentVariable("Email__Smtp__EnableSsl", value);
                            if (key == "SMTP_SENDER_ADDRESS" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__SenderAddress")))
                                Environment.SetEnvironmentVariable("Email__Smtp__SenderAddress", value);
                            if (key == "SMTP_SENDER_NAME" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__SenderName")))
                                Environment.SetEnvironmentVariable("Email__Smtp__SenderName", value);
                            if (key == "SMTP_USERNAME" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__Username")))
                                Environment.SetEnvironmentVariable("Email__Smtp__Username", value);
                            if (key == "SMTP_PASSWORD" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__Smtp__Password")))
                                Environment.SetEnvironmentVariable("Email__Smtp__Password", value);
                            if (key == "JWT_SECRET_KEY" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Jwt__SecretKey")))
                                Environment.SetEnvironmentVariable("Jwt__SecretKey", value);
                            if (key == "GOOGLE_CLIENT_ID" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Google__ClientId")))
                                Environment.SetEnvironmentVariable("Google__ClientId", value);
                        }
                        break;
                    }
                    directory = directory.Parent;
                }
            }
            catch
            {
                // Bỏ qua nếu có lỗi đọc file môi trường, tiếp tục với appsettings.json
            }
        }
    }
}
