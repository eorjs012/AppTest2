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
using System.Management;
using System.Reflection; //설치마법사 체크용

namespace AppTest2
{//요약, 상세, 페이지업, 페이지다운
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")] //페이지 업다운
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x1;
        private const int KEYEVENTF_KEYUP = 0x2;
        private const byte VK_PRIOR = 0x21; // PageUp
        private const byte VK_NEXT = 0x22;  // PageDown

        [StructLayout(LayoutKind.Sequential)] //볼륨조절///////////////////////////////
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterRawInputDevices(
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        RAWINPUTDEVICE[] pRawInputDevices,
                uint uiNumDevices,
                uint cbSize
            );
        }
        ///////////////////////////////////////////////////////////////////////////
         
        public Form1()
        {
            InitializeComponent();
            this.KeyPreview = true; // 폼이 키 이벤트 먼저 받음
            this.KeyDown += Form1_KeyDown; // 키 입력 이벤트 핸들러 등록
            this.FormClosing += Form1_FormClosing;
            RegisterK20Dial();
            // F1 키 등록 (0 = 조합키 없음)
            RegisterHotKey(this.Handle, HOTKEY_ID, 0, Keys.F1);
            
            //미니키보드 0 ~ 9 키 등록 
            RegisterHotKey(this.Handle, 9001, 0, Keys.NumPad0); //0번
            RegisterHotKey(this.Handle, 9002, 0, Keys.NumPad1); //1번
            RegisterHotKey(this.Handle, 9003, 0, Keys.NumPad2); //2번
            RegisterHotKey(this.Handle, 9004, 0, Keys.NumPad3); //3번
            RegisterHotKey(this.Handle, 9005, 0, Keys.NumPad4); //4번
            RegisterHotKey(this.Handle, 9006, 0, Keys.NumPad5); //5번
            RegisterHotKey(this.Handle, 9007, 0, Keys.NumPad6); //6번
            RegisterHotKey(this.Handle, 9008, 0, Keys.NumPad7); //7번
            RegisterHotKey(this.Handle, 9009, 0, Keys.NumPad8); //8번
            RegisterHotKey(this.Handle, 9010, 0, Keys.NumPad9); //9번

            RegisterHotKey(this.Handle, 9011, 0, Keys.Multiply); // *
            RegisterHotKey(this.Handle, 9012, 0, Keys.Subtract); // -

            contextMenuStrip1.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
            foreach (ToolStripMenuItem item in contextMenuStrip1.Items)
            {
                item.AutoSize = false;         // 자동 크기 비활성화
                item.Padding = new Padding(10, 6, 6, 6); // 글자 주변 여백
                item.Size = new Size(300, 38); // 폭 200, 높이 38 정도가 적당
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

            //설치마법사 버전확인용 버전 1
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            //MessageBox.Show($"Assembly Version: {version}");
        }

        //이미지 캡쳐 저장위치
        string baseFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
    "KEAD",
    "Screenshots");

     
        // Win32 API 선언
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, Keys vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        // 핫키 ID (중복 방지용)
        private const int HOTKEY_ID = 9000;

        private const int APPCOMMAND_VOLUME_UP = 0x0a;
        private const int APPCOMMAND_VOLUME_DOWN = 0x09;

        private void AdjustSynthVolume(int delta)
        {
            int newVolume = synth.Volume + delta;
            if (newVolume > 100) newVolume = 100;
            if (newVolume < 0) newVolume = 0;
            synth.Volume = newVolume;
            Console.WriteLine($"볼륨 조절: {synth.Volume}");
        }
        [DllImport("user32.dll")]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        private void RegisterK20Dial()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x0C;  // Consumer Page
            rid[0].usUsage = 0x01;      // Consumer Control
            rid[0].dwFlags = 0;
            rid[0].hwndTarget = this.Handle;
            NativeMethods.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }
        protected  override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
           // const int WM_APPCOMMAND = 0x0319;

            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32(); // 눌린 핫키 ID 확인

                if (id == HOTKEY_ID)
                {
                    synth.SpeakAsyncCancelAll();
                    _ = CaptureScreenAsync();
                    MessageBox.Show("F1 단축키가 눌렸습니다!", "Hotkey");
                }
                else if (id == 9001)
                {
                    MessageBox.Show("NumPad0 눌림!");
                }
                else if (id == 9002)
                {
                    MessageBox.Show("NumPad1 눌림!");
                }
                else if (id == 9003)
                {
                    MessageBox.Show("NumPad2 눌림!");
                }
                else if (id == 9004)
                {
                    MessageBox.Show("NumPad3 눌림!");
                }
                else if (id == 9005)
                {
                    MessageBox.Show("NumPad4 눌림!");
                }
                else if (id == 9006)
                {
                    MessageBox.Show("NumPad5 눌림!");
                }
                else if (id == 9007)
                {
                    MessageBox.Show("NumPad6 눌림!");
                }
                else if (id == 9008)
                {
                    MessageBox.Show("NumPad7 눌림!");
                }
                else if (id == 9009)
                {
                    MessageBox.Show("NumPad8 눌림!");
                }
                else if (id == 9010)
                {
                    MessageBox.Show("NumPad9 눌림!");
                }
                else if (id == 9011) // 곱하기(*) → PageUp
                {
                    keybd_event(VK_PRIOR, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    keybd_event(VK_PRIOR, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                else if (id == 9012) // 마이너스(-) → PageDown
                {
                    keybd_event(VK_NEXT, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    keybd_event(VK_NEXT, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            /*else if (m.Msg == WM_APPCOMMAND)
            {
                int cmd = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                switch (cmd)
                {
                    case APPCOMMAND_VOLUME_UP:
                        AdjustSynthVolume(+5); // 5씩 올림
                        break;
                    case APPCOMMAND_VOLUME_DOWN:
                        AdjustSynthVolume(-5); // 5씩 내림
                        break;
                }
            }*/
            base.WndProc(ref m);
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 폼 닫아도 핫키 유지하려면 완전히 종료하지 않고 숨김
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            // 폼이 완전히 종료될 때 핫키 해제
            try { UnregisterHotKey(this.Handle, HOTKEY_ID); }
            catch { }
            base.OnHandleDestroyed(e);
        }


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
                //CapturePartialScreen(); // 부분 화면 캡쳐
            }
            else if (e.KeyCode == Keys.F3)
            {
                CaptureAllMonitors();
            }
        }
        
        private string cachedApiToken = null;

        //화면캡쳐
        public async Task CaptureScreenAsync()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            using (Bitmap bmp = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screen.Left, screen.Top, 0, 0, bmp.Size);
                }

                ////////////추후 삭제///////////////////
                pictureBox1.Image = (Bitmap)bmp.Clone();
                /////////////추후 삭제///////////////////

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
                        cachedApiToken = token;
                }

                if (!string.IsNullOrEmpty(token))
                {
                   await SendImageToServerAsync(base64Image, token, "summarize"); //요약
                  //await SendImageToServerAsync(base64Image, token, "read-main"); //전체

                }
                else
                {
                    MessageBox.Show("토큰 발급 실패로 이미지 전송이 취소되었습니다.");
                }
            }
        }

        private async Task SendImageToServerAsync(string base64Image, string token, string mode)
        {
            using (HttpClient client = new HttpClient())
            {
                var url = "http://222.109.31.211/api/v1/screen/stream-analysis";
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var payload = new
                {
                    image = base64Image,
                    mode = mode, // "summarize" 또는 "read-main"
                    //provider = "local"
                    provider = "openai"
                    //provider = "google"
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await client.PostAsync(url, content))
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    var synth = new SpeechSynthesizer();
                    synth.Rate = 9; // 9 기본값
                    synth.Volume = 100;

                    Console.WriteLine("화면 분석 스트리밍 시작");

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("data:"))
                            line = line.Substring(5).Trim();

                        try
                        {
                            var msg = JsonConvert.DeserializeObject<dynamic>(line);
                            string type = msg?.type;
                            string contentText = msg?.content;

                            if (type == "message" && !string.IsNullOrEmpty(contentText))
                            {
                                synth.SpeakAsync(contentText);
                                Console.WriteLine($"읽기: {contentText}");
                            }
                            else
                            {
                                Console.WriteLine($"상태: {contentText}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[파싱오류] {ex.Message}");
                            Console.WriteLine($"원본: {line}");
                        }
                    }
                }
            }
        }

        //버젼을 찾을 수 없음 
        private async Task<string> GetServerVersionAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var versionUrl = "http://222.109.31.211/api/v1/auth/register";
                    HttpResponseMessage response = await client.GetAsync(versionUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();

                        dynamic json = JsonConvert.DeserializeObject(responseBody);
                        string version = json.version;

                        Debug.WriteLine($"서버에서 가져온 버전: {version}");
                        return version;
                    }
                    else
                    {
                        Debug.WriteLine($"버전 조회 실패: {response.StatusCode}");
                        return "UNKNOWN_VERSION";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"버전 조회 오류: {ex.Message}");
                return "UNKNOWN_VERSION";
            }
        }
        private async Task<string> RegisterClientAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var url = "http://222.109.31.211/api/v1/auth/register";

                    string[] serialKeys =
                    {
                        "SK-7EAIA-S1ILF-RJY8D",
                        "SK-H0BVC-LSP2Q-PZKVS",
                        "SK-H2HKF-716EA-E6P3N",
                        "SK-9C5QY-XTTFO-ENQYN",
                        "SK-EH726-55RBX-TJM4I",
                        "SK-E41E2-6GFJ7-5PTK9",
                        "SK-DTHW9-ZQ26J-SI4M5",
                        "SK-56KWU-2JLZA-XVQTW",
                        "SK-GS1V3-74V2T-VOW1G",
                        "SK-Z69BK-Z8SXT-AZ5WL"
                    };

                    // 랜덤 키 선택 (또는 특정 조건에 맞게 선택 가능)  서버에서 만료되어있는지 체크
                    Random rnd = new Random();
                    string serialKey = serialKeys[rnd.Next(serialKeys.Length)];

                    var payload = new
                    {
                        serial_key = serialKey,
                        client_version = "1.0.0",
                        os = Environment.OSVersion.ToString(),
                        os_type = "Windows",
                        os_build = Environment.OSVersion.Version.Build.ToString(),
                        architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
                    };

                    string jsonString = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] Register Response: {responseBody}");
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
        /*
        //하드웨어 시리얼 키
        private string GetHarddiskSerial()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

                foreach (ManagementObject wmi_HD in searcher.Get())
                {
                    string serialNumber = wmi_HD["SerialNumber"]?.ToString().Trim();
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        Console.WriteLine($"디스크 시리얼: {serialNumber}");
                        return serialNumber;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("하드디스크 시리얼 읽기 오류: " + ex.Message);
            }

            return null;
        }
        */

        /*

        //이미지 전송 string 호출
        private async Task SendImageToServerAsync(string base64Image, string token)
        {
            try
            {   
                using (HttpClient client = new HttpClient())
                {
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
                        // 응답 내용이 JSON 형태인지 확인
                        try
                        {   
                            dynamic result = JsonConvert.DeserializeObject(responseBody);
                            // 예: 서버가 문자열 응답을 반환하는 경우
                            string resultText = result != null ? result.ToString() : "(null)";
                            MessageBox.Show($" 이미지 전송 성공!\n\n응답 내용:\n{resultText}");
                            Console.WriteLine(base64Image);
                        }
                        catch (JsonException)
                        {
                            // JSON이 아닐 경우 그대로 출력
                            MessageBox.Show($" 이미지 전송 성공!n\n{responseBody}");
                            Console.WriteLine(base64Image);
                        }
                    }
                    else
                    {
                        MessageBox.Show($" 전송 실패: {response.StatusCode}\n\n응답 내용:\n{responseBody}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 전송 중 오류 발생:\n{ex.Message}");
                Debug.WriteLine(ex);
            }
        }
        */
        /*****테스트용*****/
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
        private void CaptureAllMonitors() //듀얼모니터
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

        //윈도우 TTS 9로 시작
        private SpeechSynthesizer synth = new SpeechSynthesizer();
        private void button8_Click(object sender, EventArgs e) //TTS 
        {
            synth.Rate = 9;   // 말하기 속도 (기본 0, -10 ~ 10)
            synth.Volume = 100; // 볼륨 (0 ~ 100)
            synth.SpeakAsync("안녕하세요.테스트 중입니다.안녕하세요.테스트 중입니다.안녕하세요.테스트 중입니다.안녕하세요.테스트 중입니다.안녕하세요.테스트 중입니다.");
        }

        private void button9_Click(object sender, EventArgs e) //TTS STOPㄴ
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
