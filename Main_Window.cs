using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Debug_checkUpdate
{
    public partial class Main_Window: Form
    {
        //变量========================================================================================
        public static string Version = "Release1.0.0.0";
        //函数========================================================================================
        //写配置项
        public void WriteConfig(string filePath, string key, string value)
        {
            // 检查文件是否存在，如果不存在则创建
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }

            // 读取所有行
            var lines = File.ReadAllLines(filePath);
            var newLines = new List<string>();

            bool keyExists = false;
            foreach (var line in lines)
            {
                // 如果键存在，则更新值
                if (line.StartsWith(key + "="))
                {
                    newLines.Add(key + "=" + value);
                    keyExists = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }

            // 如果键不存在，则添加新键值对
            if (!keyExists)
            {
                newLines.Add(key + "=" + value);
            }

            // 写回文件
            File.WriteAllLines(filePath, newLines);
        }

        //读配置项
        public string ReadConfig(string filePath, string key)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("配置文件未找到", filePath);
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.StartsWith(key + "="))
                {
                    return line.Substring(key.Length + 1);
                }
            }

            throw new KeyNotFoundException($"键 {key} 未找到");
        }

        //HTTP读取文件(同步)
        public static string HttpReadFile(string url)
        {
            try
            {
                // 设置安全协议类型（支持TLS 1.2/1.1/1.0）
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls11 |
                    SecurityProtocolType.Tls;

                // 创建带自定义验证的HttpClient
                using (var handler = new HttpClientHandler())
                using (var client = new HttpClient(handler))
                {
                    // 忽略SSL证书验证
                    handler.ServerCertificateCustomValidationCallback =
                        (sender, cert, chain, sslPolicyErrors) => true;

                    // 设置超时时间（10秒）
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // 添加浏览器User-Agent
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                    // 发送GET请求
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();

                    // 读取字节内容
                    var bytes = response.Content.ReadAsByteArrayAsync().Result;

                    // 检测编码
                    var encoding = DetectEncoding(response, bytes);

                    // 转换为字符串
                    return encoding.GetString(bytes);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        //HTTPS读文件(检测编码)
        private static Encoding DetectEncoding(HttpResponseMessage response, byte[] bytes)
        {
            try
            {
                // 从Content-Type头获取编码
                var contentType = response.Content.Headers.ContentType;
                if (contentType?.CharSet != null)
                {
                    return Encoding.GetEncoding(contentType.CharSet);
                }
            }
            catch
            {
                // 忽略编码解析错误
            }

            // 尝试通过BOM检测编码
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;

            // 默认使用UTF-8
            return Encoding.UTF8;
        }

        //写日志
        public static void Log(string level, string message)
        {
            // 获取当前时间并格式化
            string timestamp = DateTime.Now.ToString("HH:mm:ss");

            // 构造完整日志条目
            string logContent = $"[{timestamp}][{level}]: {message}";

            // 拼接完整文件路径
            string logPath = Path.Combine(Application.StartupPath, "Log.log");

            // 使用追加模式写入文件
            using (StreamWriter sw = new StreamWriter(logPath, true))
            {
                sw.WriteLine(logContent);
            }

        }

        //文件写一行
        public static void FileAddLine(string content, string filePath)
        {
            using (StreamWriter sw = File.AppendText(filePath))
            {
                sw.WriteLine(content);
            }
        }

        //连通性测试
        public static object CheckUrlConnection(string url)
        {
            // 验证URL格式有效性
            try
            {
                var uri = new Uri(url);
            }
            catch (UriFormatException)
            {
                return "unconnect";
            }

            HttpWebRequest request = null;
            Stopwatch sw = new Stopwatch();

            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 5000;     // 设置5秒超时
                request.Method = "HEAD";     // 使用HEAD方法减少数据量

                sw.Start();
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    sw.Stop();
                    return sw.ElapsedMilliseconds;
                }
            }
            catch (WebException ex)
            {
                sw.Stop();
                /* 服务器响应但返回错误状态（如404）的情况
                   仍视为连接成功，返回延迟时间 */
                if (ex.Response != null)
                {
                    return sw.ElapsedMilliseconds;
                }
                return "unconnect"; // 真正无法连接的情况
            }
            catch (Exception)
            {
                return "unconnect";
            }
            finally
            {
                request?.Abort(); // 确保释放网络资源
            }
        }

        //执行控制台命令
        public string ExecuteCommand(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = false,          // 不创建新窗口
                    UseShellExecute = false,        // 不使用系统外壳程序执行
                    RedirectStandardError = true,   // 重定向标准错误
                    RedirectStandardOutput = true   // 重定向标准输出
                };

                using (var process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.Start();

                    // 异步读取输出流和错误流
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();  // 等待程序执行完成

                    // 组合输出结果
                    string result = string.IsNullOrEmpty(output) ? "" : output;
                    string errorResult = string.IsNullOrEmpty(error) ? "" : "\n[Error]\n" + error;

                    return $"{result}{errorResult} (ExitCode: {process.ExitCode})";
                }
            }
            catch (Exception ex)
            {
                return $"执行命令时发生异常：{ex.Message}";
            }
        }

        //搜索文件内容
        public bool FileContainsText(string filePath, string searchText)
        {
            try
            {
                // 检查搜索文本是否有效
                if (string.IsNullOrEmpty(searchText))
                    return false;

                // 读取文件全部内容
                string fileContent = File.ReadAllText(filePath);

                // 检查内容是否包含目标文本
                return fileContent.Contains(searchText);
            }
            catch (Exception ex) when (ex is FileNotFoundException ||
                                      ex is IOException ||
                                      ex is UnauthorizedAccessException)
            {
                // 处理常见文件异常：文件不存在、无法访问或IO错误
                return false;
            }
        }
        //构造函数======================================================================================
        public Main_Window()
        {
            InitializeComponent();
        }
        //事件========================================================================================
        //窗口Main_Window 加载

        private void Main_Window_Load(object sender, EventArgs e)
        {

        }
    }
}
