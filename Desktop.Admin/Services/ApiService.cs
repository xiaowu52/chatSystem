using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Web.API.Models;
using System.Text;
using System.Security.Cryptography;

namespace Desktop.Admin.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string _baseUrl = "https://localhost:5284/api";

        public ApiService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // 从应用程序属性中获取token
            if (App.Current.Properties.Contains("Token"))
            {
                var token = App.Current.Properties["Token"].ToString();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // 添加设置Token的方法
        public void SetToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // 保存token到应用程序属性
            App.Current.Properties["Token"] = token;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public async Task<T> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/{endpoint}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到服务器: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"解析响应失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"请求失败: {ex.Message}", ex);
            }
        }

        public async Task<T> PostAsync<T>(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/{endpoint}", content);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到服务器: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"解析响应失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"请求失败: {ex.Message}", ex);
            }
        }

        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}/{endpoint}", content);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到服务器: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"解析响应失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"请求失败: {ex.Message}", ex);
            }
        }

        public async Task DeleteAsync(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/{endpoint}");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"无法连接到服务器: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"请求失败: {ex.Message}", ex);
            }
        }

        // 用户管理相关方法
        public async Task ApproveUser(int userId)
        {
            await PostAsync<object>("admin/approve", new { userId });
        }

        public async Task BanUser(int userId)
        {
            await PostAsync<object>("admin/ban", new { userId });
        }

        public async Task UnbanUser(int userId)
        {
            await PostAsync<object>("admin/unban", new { userId });
        }

        // 消息管理相关方法
        public async Task DeleteMessage(int messageId)
        {
            await DeleteAsync($"admin/messages/{messageId}");
        }

        // 登录相关方法
        public async Task<string> Login(string username, string password)
        {
            var response = await PostAsync<JsonElement>("admin/token", new { username, password});
            if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("token", out var tokenElement))
            {
                var token = tokenElement.GetString();
                if (!string.IsNullOrEmpty(token))
                {
                    SetToken(token);
                    return token;
                }
            }
            throw new Exception("登录失败：无效的响应格式");
        }
    }
}