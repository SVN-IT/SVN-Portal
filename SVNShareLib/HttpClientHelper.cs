using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public interface IHttpClientHelper<T>
    {
        void ToAuthHeaderValue(string username, string password);
        void ToAuthHeaderValue(string token);
        Task<T> GetSingleItemRequest(string apiUrl, CancellationToken token = default(CancellationToken));
        Task<T[]> GetMultipleItemsRequest(string apiUrl, CancellationToken token = default(CancellationToken));
        Task<T> PostRequest(string apiUrl, object postObject, CancellationToken token = default(CancellationToken));
        Task PutRequest(string apiUrl, object putObject, CancellationToken token = default(CancellationToken));
        Task DeleteRequest(string apiUrl, CancellationToken token = default(CancellationToken));
    }
    public class HttpClientHelper<T> : IHttpClientHelper<T>
    {
        /// <summary>
        /// add authorized by user/pwd
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void ToAuthHeaderValue(string username, string password)
        {

            client.DefaultRequestHeaders.Authorization =
new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes(
                $"{username}:{password}")));

        }

        //addauthorized by token
        public void ToAuthHeaderValue(string token)
        {
            client.DefaultRequestHeaders.Authorization =
 new AuthenticationHeaderValue("Bearer", token);
        }
        public HttpClientHelper(string baseAddress, int timeOut)
        {

            client = new HttpClient(GetInsecureHandler());
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(timeOut);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));


        }
        public HttpClientHandler GetInsecureHandler()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert.Issuer.Equals("CN=localhost"))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            return handler;
        }
        private static HttpClient client;
        /// <summary>
        /// For getting a single item from a web api uaing GET
        /// </summary>
        /// <param name="apiUrl">Added to the base address to make the full url of the 
        ///     api get method, e.g. "products/1" to get a product with an id of 1</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The item requested</returns>
        public async Task<T> GetSingleItemRequest(string apiUrl, CancellationToken cancellationToken)
        {
            var result = default(T);
            var response = await client.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await response.Content.ReadAsStringAsync().ContinueWith(x =>
                {
                    if (typeof(T).Namespace != "System")
                    {
                        result = JsonConvert.DeserializeObject<T>(x?.Result);
                    }
                    else result = (T)Convert.ChangeType(x?.Result, typeof(T));
                }, cancellationToken);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                response.Content?.Dispose();
                throw new HttpRequestException($"{response.StatusCode}:{content}");
            }
            return result;
        }

        /// <summary>
        /// For getting multiple (or all) items from a web api using GET
        /// </summary>
        /// <param name="apiUrl">Added to the base address to make the full url of the 
        ///     api get method, e.g. "products?page=1" to get page 1 of the products</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The items requested</returns>
        public async Task<T[]> GetMultipleItemsRequest(string apiUrl, CancellationToken cancellationToken)
        {
            T[] result = null;
            var response = await client.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await response.Content.ReadAsStringAsync().ContinueWith((Task<string> x) =>
                {
                    result = JsonConvert.DeserializeObject<T[]>(x.Result);
                }, cancellationToken);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                response.Content?.Dispose();
                throw new HttpRequestException($"{response.StatusCode}:{content}");
            }
            return result;
        }

        /// <summary>
        /// For creating a new item over a web api using POST
        /// </summary>
        /// <param name="apiUrl">Added to the base address to make the full url of the 
        ///     api post method, e.g. "products" to add products</param>
        /// <param name="postObject">The object to be created</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The item created</returns>
        public async Task<T> PostRequest(string apiUrl, object postObject, CancellationToken cancellationToken)
        {
            T result = default(T);
            var json = JsonConvert.SerializeObject(postObject);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            //new JsonContent(postObject)
            try
            {
                var response = await client.PostAsync(apiUrl, data, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {

                    await response.Content.ReadAsStringAsync().ContinueWith((Task<string> x) =>
                    {
                        result = JsonConvert.DeserializeObject<T>(x.Result);
                    }, cancellationToken);
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    response.Content?.Dispose();
                    //throw new HttpRequestException($"{response.StatusCode}:{content}");
                }
            }
            catch
            {
                throw;
            }
            //var response = await client.PostAsync(apiUrl, data, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// For updating an existing item over a web api using PUT
        /// </summary>
        /// <param name="apiUrl">Added to the base address to make the full url of the 
        ///     api put method, e.g. "products/3" to update product with id of 3</param>
        /// <param name="putObject">The object to be edited</param>
        /// <param name="cancellationToken"></param>
        public async Task PutRequest(string apiUrl, object putObject, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(putObject);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync(apiUrl, data, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                response.Content?.Dispose();
                throw new HttpRequestException($"{response.StatusCode}:{content}");
            }
        }

        /// <summary>
        /// For deleting an existing item over a web api using DELETE
        /// </summary>
        /// <param name="apiUrl">Added to the base address to make the full url of the 
        ///     api delete method, e.g. "products/3" to delete product with id of 3</param>
        /// <param name="cancellationToken"></param>
        public async Task DeleteRequest(string apiUrl, CancellationToken cancellationToken)
        {
            var response = await client.DeleteAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                response.Content?.Dispose();
                throw new HttpRequestException($"{response.StatusCode}:{content}");
            }
        }
    }
}
