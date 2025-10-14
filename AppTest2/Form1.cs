using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net;
using System.Speech.Synthesis;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json;
using System.Diagnostics;

namespace AppTest2
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();
            this.KeyPreview = true; // 폼이 키 이벤트 먼저 받음
            this.KeyDown += Form1_KeyDown; // 키 입력 이벤트 핸들러 등록
            contextMenuStrip1.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
            foreach (ToolStripMenuItem item in contextMenuStrip1.Items)
            {
                item.AutoSize = false;         // 자동 크기 비활성화
                item.Padding = new Padding(6, 6, 6, 6); // 글자 주변 여백
                item.Size = new Size(200, 38); // 폭 200, 높이 38 정도가 적당
            }
            contextMenuStrip1.ShowImageMargin = false;
            contextMenuStrip1.BackColor = Color.White;
            contextMenuStrip1.ForeColor = Color.Black;

            // 폴더 없으면 생성
            if (!Directory.Exists(baseFolder))
                Directory.CreateDirectory(baseFolder);

            // 설치 시 넣어둔 readme.txt 삭제
            string dummyFile = Path.Combine(baseFolder, "readme.txt");
            if (File.Exists(dummyFile))
            {
                try { File.Delete(dummyFile); }
                catch { /* 실패해도 무시 */ }
            }

            // 하루 지난 캡쳐 자동 삭제
            //CleanOldScreenshots();
        }
        string baseFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
    "KEAD",
    "Screenshots");
        private async void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                await CaptureScreenAsync();
                //CapturePrimaryMonitor();
                /*
                if (Screen.AllScreens.Length > 1)
                {
                    CaptureAllMonitors(); // 듀얼 이상일 때 전체 캡쳐
                }
                else
                {
                    CapturePrimaryMonitor(); // 단일 모니터 캡쳐
                }
                //CaptureFullScreen(); // 전체 화면 캡쳐
                */
            }
            else if (e.KeyCode == Keys.F2)
            {
                CapturePartialScreen(); // 부분 화면 캡쳐
            }
            else if (e.KeyCode == Keys.F3)
            {
                CaptureAllMonitors();
            }
        }
















        private string cachedApiToken = null;
        //화면캡쳐
        private async Task CaptureScreenAsync()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            using (Bitmap bmp = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screen.Left, screen.Top, 0, 0, bmp.Size);
                }

                pictureBox1.Image = (Bitmap)bmp.Clone();

                string todayFolder = Path.Combine(baseFolder, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(todayFolder))
                    Directory.CreateDirectory(todayFolder);

                string fileName = DateTime.Now.ToString("dd_HHmmss") + ".png";
                string fullPath = Path.Combine(todayFolder, fileName);
                bmp.Save(fullPath, ImageFormat.Png);
                //MessageBox.Show($"캡처 완료: {fullPath}");

                // 파일별 삭제 타이머
                //await DeleteFileAfterDelay(fullPath, TimeSpan.FromDays(1));
                _ = DeleteFileAfterDelay(fullPath, TimeSpan.FromDays(1));
                // 1. Base64 변환
                string base64Image;
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    base64Image = Convert.ToBase64String(ms.ToArray());
                    MessageBox.Show($"Base64 변환 완료!\n길이: {base64Image.Length}");
                }

                // 2. 서버로 전송
                string token = cachedApiToken;
                if (string.IsNullOrEmpty(token))
                {
                    token = await RegisterClientAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        cachedApiToken = token; // 캐시 저장 (앱이 켜져있는 동안 재사용)
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    await SendImageToServerAsync(base64Image, token);
                }
                else
                {
                    MessageBox.Show("⚠️ 토큰을 얻지 못해 이미지 전송을 취소합니다.");
                }

                //              MessageBox.Show(base64Image);
                //            Debug.WriteLine(base64Image);

            }
        }


        //이미지 전송
        private async Task SendImageToServerAsync(string base64Image, string token)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                  /*  var url = "http://222.109.31.211/api/v1";

                    var payload = new { image = base64Image };
                    string jsonString = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("이미지 전송 성공!");
                    }
                    else
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"전송 실패: {response.StatusCode}\n서버 응답: {responseBody}");
                    }*/
                    var url = "http://222.109.31.211/api/v1/screen/stream-analysis";
                    client.DefaultRequestHeaders.Authorization =
                         new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var payload = new { image = base64Image };
                    string jsonString = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($" 이미지 전송 성공!\n응답: {responseBody}");
                    }
                    else
                    {
                        MessageBox.Show($" 전송 실패: {response.StatusCode}\n응답 내용: {responseBody}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 전송 중 오류 발생:\n{ex.Message}");
                Debug.WriteLine(ex);
            }
            /*
            using (HttpClient client = new HttpClient())
            {
                var url = "http://222.109.31.211/api/v1";

                // 변수 이름 바꿔서 중복 방지
                var payload = new { image = base64Image };
             
                string jsonString = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("이미지 전송 성공!");
                    Debug.WriteLine(response.IsSuccessStatusCode);
                    Debug.WriteLine(response.StatusCode);
                    Console.WriteLine(response.IsSuccessStatusCode);
                    Console.WriteLine(response.StatusCode);
                }
                else
                {
                    MessageBox.Show($"전송 실패: {response.StatusCode}");
                    Debug.WriteLine(response.IsSuccessStatusCode);
                    Debug.WriteLine(response.StatusCode);
                    Console.WriteLine(response.IsSuccessStatusCode);
                    Console.WriteLine(response.StatusCode);
                }
            }*/
        }

        private async Task<string> RegisterClientAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var url = "http://222.109.31.211/api/v1/auth/register";

                    var payload = new
                    {
                        serial_key = "MY-CLIENT-1234",
                        client_version = "1.0.0",
                        os = Environment.OSVersion.ToString(),
                        architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
                    };

                    string jsonString = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic result = JsonConvert.DeserializeObject(responseBody);
                        string apiToken = result.api_token;
                        MessageBox.Show($"토큰 발급 완료!\n{apiToken}");
                        return apiToken;
                    }
                    else
                    {
                        MessageBox.Show($"토큰 발급 실패: {response.StatusCode}\n{responseBody}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"토큰 발급 중 오류: {ex.Message}");
                Debug.WriteLine(ex);
                return null;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var response = await client.GetAsync("http://222.109.31.211/api/v1/auth/validate");
                return response.IsSuccessStatusCode;
            }
        }
        private async Task SendImageToServerAsyncs(string base64Image)
        {
            string token = File.Exists("token.txt") ? File.ReadAllText("token.txt") : null;

            if (string.IsNullOrEmpty(token) || !await ValidateTokenAsync(token))
            {
                token = await RegisterClientAsync();
            }

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                var url = "http://222.109.31.211/api/v1";
                var payload = new { image = base64Image };
                string jsonString = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("이미지 전송 성공!");
                }
                else
                {
                    MessageBox.Show($"전송 실패: {response.StatusCode}");
                }
            }
        }
      
        private void CaptureFullScreen()
        {

            Rectangle bounds = Screen.PrimaryScreen.Bounds;

            using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                pictureBox1.Image = (Bitmap)bmp.Clone(); // 미리보기로 보여줌
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"full_capture_{DateTime.Now.Ticks}.png");
                bmp.Save(path);
                MessageBox.Show("전체 화면 캡쳐 저장됨:\n" + path);
            }
        }

        private void CapturePartialScreen() // 키값 부분화면캡쳐
        {  
            using (CaptureForm captureForm = new CaptureForm())
            {
                if (captureForm.ShowDialog() == DialogResult.OK)
                { 
                    Rectangle rect = captureForm.SelectedRegion;

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        using (Bitmap bmp = new Bitmap(rect.Width, rect.Height))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                            }

                            pictureBox1.Image = (Bitmap)bmp.Clone();

                            string path = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                $"partial_capture_{DateTime.Now.Ticks}.png");
                            bmp.Save(path);

                            MessageBox.Show("부분 화면 캡쳐 저장됨:\n" + path);
                        }
                    } 
                }
            }

        }
        private Rectangle GetVirtualScreenBounds() //모니터 전체 영역 계산 함수
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Bounds.Left < minX) minX = screen.Bounds.Left;
                if (screen.Bounds.Top < minY) minY = screen.Bounds.Top;
                if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
        private void CaptureAllMonitors()
        {
            Rectangle totalBounds = GetVirtualScreenBounds();

            using (Bitmap bmp = new Bitmap(totalBounds.Width, totalBounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(totalBounds.Location, Point.Empty, totalBounds.Size);
                }

                pictureBox1.Image = (Bitmap)bmp.Clone();

                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"all_monitors_capture_{DateTime.Now.Ticks}.png");
                bmp.Save(path);

                MessageBox.Show("전체 모니터 화면 캡쳐 저장됨:\n" + path);
            }
        }
        private void CapturePrimaryMonitor()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;

            using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                pictureBox1.Image = (Bitmap)bmp.Clone();

                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"primary_monitor_capture_{DateTime.Now.Ticks}.png");
                bmp.Save(path);

                MessageBox.Show("주 모니터 화면 캡쳐 저장됨:\n" + path);
            }
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            using (CaptureForm captureForm = new CaptureForm())
            {
                if (captureForm.ShowDialog() == DialogResult.OK)
                {
                    Rectangle rect = captureForm.SelectedRegion;

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        Bitmap bmp = new Bitmap(rect.Width, rect.Height);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                        }

                        pictureBox1.Image = bmp;

                        string path = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                            $"capture_{DateTime.Now.Ticks}.png");
                        bmp.Save(path);

                        MessageBox.Show("캡쳐 저장됨:\n" + path);
                    }
                }
            }
        }
      
        

        private async void button4_Click(object sender, EventArgs e)//전체화면캡쳐 DPI 완료
        {
            await CaptureScreenAsync();
           
        }

        private async Task DeleteFileAfterDelay(string filePath, TimeSpan delay)
        {
            await Task.Delay(delay); // 지정된 시간 대기

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"삭제됨: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"파일 삭제 실패 ({filePath}): {ex.Message}");
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            
        }
        Bitmap btMain;

        private void button5_Click(object sender, EventArgs e) //화면전체캡쳐 테스트
        {
            btMain = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(btMain))
            {
                g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, btMain.Size, CopyPixelOperation.SourceCopy);
                //Picture Box Display
                pictureBox1.Image = btMain;
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
      
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if(btMain != null)
            {
                SaveFileDialog sfd = new SaveFileDialog();

                sfd.Filter = "JPG File(*,jpg) | *.jpg";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    btMain.Save(sfd.FileName);
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e) //프로그램 이름 
        {
            this.Text = Application.ProductName + " Ver " + Application.ProductVersion; // 앱 + VER + 버전정보
        }
        private bool result = false;
        //private FTPClass fTP = null;

        private void ConnectionBtn_Click(object sender, EventArgs e)
        {
            
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e) //부분화면캡쳐
        {
            using (CaptureForm captureForm = new CaptureForm())
            {
                if (captureForm.ShowDialog() == DialogResult.OK)
                {
                    Rectangle rect = captureForm.SelectedRegion;

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        using (Bitmap bmp = new Bitmap(rect.Width, rect.Height))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                            }

                            pictureBox1.Image = (Bitmap)bmp.Clone();

                            string path = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                $"partial_capture_{DateTime.Now.Ticks}.png");
                            bmp.Save(path);

                            MessageBox.Show("부분 화면 캡쳐 저장됨:\n" + path);
                        }
                    }
                }
            }
        }
        private SpeechSynthesizer synth = new SpeechSynthesizer();
        private void button8_Click(object sender, EventArgs e) //TTS 
        {
            synth.Rate = -10;   // 말하기 속도 (기본 0, -10 ~ 10)
            synth.Volume = 100; // 볼륨 (0 ~ 100)
            synth.SpeakAsync("안녕하세요.테스트 중입니다.");
        }

        private void button9_Click(object sender, EventArgs e) //TTS STOP
        {
            synth.SpeakAsyncCancelAll(); // 말하기 중지
        }

        // 폼이 닫힐때 발생하는 이벤트 : 백그라운드 실행
        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            if(e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void ContextMenuStrip_Open(object sender, EventArgs e)
        {
            this.Show();
        }

        private void ContextMenuStrip_Exit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /*
         * string apiUrl = "https://your-server.com/api/v1/screen/stream-analysis";

using (HttpClient client = new HttpClient())
{
    // Bearer Token 인증
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

    var body = new { image = encryptedBase64 }; // 암호화+Base64 된 이미지
    string json = System.Text.Json.JsonSerializer.Serialize(body);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    // POST 요청
    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

    // 스트리밍 응답 읽기
    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);
    while (!reader.EndOfStream)
    {
        string line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) continue;

        if (line.StartsWith("data:"))
        {
            string jsonLine = line.Substring(5);
            Console.WriteLine("수신: " + jsonLine);
        }
    }
}
         * */
            }
        }
