using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spielwiese.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args == null || args.Length != 2 || args[0] != "--target")
            {
                Console.WriteLine(@"Aufruf: dotnet .\TryMultipleRequests.dll --target http://server-url.de");
                Console.WriteLine("Es wird davon ausgegangen, dass der accounts-Endpunkt sich in der Route /api/accounts befindet.");
                return;
            }

            if (!Uri.TryCreate(args[1], UriKind.Absolute, out Uri uri) && !Uri.TryCreate("http://" + args[1], UriKind.Absolute, out uri))
            {
                Console.WriteLine($"{args[1]} wurde nicht als absolute URI erkannt.");
                return;
            }

            Console.Write("Wie viele Requests sollen gleichzeitig abgesetzt werden (1-30): ");
            if (!uint.TryParse(Console.ReadLine(), out uint taskCount) || taskCount > 30)
            {
                Console.WriteLine("Dann nicht.");
                return;
            }

            var handler = new AccountHandler(uri);
            var success = 0;
            var failed = 0;

            var tasks = Enumerable.Range(1, (int)taskCount).Select(async p =>
            {
                var token = await handler.Login();
                if (token != null)
                {
                    if (!await handler.Logout(token))
                    {
                        failed++;
                    }
                    else
                    {
                        success++;
                    }

                }
                else
                {
                    failed++;
                }

            });

            await Task.WhenAll(tasks);
            Console.WriteLine($"{success}/{taskCount} erfolgreich.");
        }
    }

    class AccountHandler
    {
        private readonly Uri _baseAddress;

        public AccountHandler(Uri baseAddress)
        {
            _baseAddress = baseAddress;
        }

        public async Task<string> Login()
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, GetAddress("accounts")))
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(new { Login = "wilma.winzig@mail.de", Password = "wilma" }));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.Add("x-sts-APIKey", "5567GGH8635B!83GhT!j75%");
                var result = await client.SendAsync(request);
                if (!result.IsSuccessStatusCode) return null;

                using (var streamReader = new StreamReader(await result.Content.ReadAsStreamAsync()))
                using (var reader = new JsonTextReader(streamReader))
                {
                    var jObject = await JObject.LoadAsync(reader);
                    return jObject["Token"].Value<string>();
                }
            }
        }

        public async Task<bool> Logout(string token)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Delete, GetAddress("accounts")))
            {
                request.Headers.Add("x-sts-APIKey", "5567GGH8635B!83GhT!j75%");
                client.DefaultRequestHeaders.TryAddWithoutValidation(HttpRequestHeader.Authorization.ToString(), token);

                var result = await client.SendAsync(request);
                return result.IsSuccessStatusCode;
            }
        }

        private Uri GetAddress(string endpoint) => new Uri(_baseAddress, $"api/{endpoint}");
    }
}
