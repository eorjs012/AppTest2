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
using System.Reflection;

namespace AppTest2
{
    public partial class Form1 : Form
    {
        /*
         * 이 Form은 다음 기능을 담당함
         * - 전역 핫키 등록 (미니 키보드)
         * - 화면 캡처 (전체 / 요약)
         * - 서버 전송 및 스트리밍 응답 처리
         * - TTS 큐 기반 음성 출력
         * - 백그라운드 실행 (트레이 형태)
         */

      
        // Win32 키 이벤트 호출 (PageUp / PageDown 에뮬레이션)
        [DllImport("user32.dll")] 
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // 키 플래그
        private const int KEYEVENTF_EXTENDEDKEY = 0x1;
        private const int KEYEVENTF_KEYUP = 0x2;

        // 가상 키 코드
        private const byte VK_PRIOR = 0x21; // PageUp
        private const byte VK_NEXT = 0x22;  // PageDown


        // RAW INPUT 구조체 (하드웨어 다이얼, 볼륨 장치용)
        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }


        // RAW INPUT 등록용 Native 메서드
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


        public Form1()
        {
            InitializeComponent();

            // 폼이 키 이벤트를 먼저 받도록 설정
            this.KeyPreview = true;

            // 닫기 버튼 눌러도 종료되지 않도록 처리
            this.FormClosing += Form1_FormClosing;

            // 하드웨어 다이얼 등록
            RegisterK20Dial();

            // TTS 기본 설정
            synth.Rate = 9; 
            synth.Volume = 100;


            // 미니 키보드 숫자키 전역 핫키 등록
            RegisterHotKey(this.Handle, 9001, 0, Keys.NumPad0); 
            RegisterHotKey(this.Handle, 9002, 0, Keys.NumPad1); 
            RegisterHotKey(this.Handle, 9003, 0, Keys.NumPad2);
            RegisterHotKey(this.Handle, 9004, 0, Keys.NumPad3);
            RegisterHotKey(this.Handle, 9005, 0, Keys.NumPad4);
            RegisterHotKey(this.Handle, 9006, 0, Keys.NumPad5);
            RegisterHotKey(this.Handle, 9007, 0, Keys.NumPad6);
            RegisterHotKey(this.Handle, 9008, 0, Keys.NumPad7);
            RegisterHotKey(this.Handle, 9009, 0, Keys.NumPad8);
            RegisterHotKey(this.Handle, 9010, 0, Keys.NumPad9);
            RegisterHotKey(this.Handle, 9011, 0, Keys.NumLock);
            RegisterHotKey(this.Handle, 9012, 0, Keys.Divide); 
            RegisterHotKey(this.Handle, 9013, 0, Keys.Multiply); 
            RegisterHotKey(this.Handle, 9014, 0, Keys.Subtract);
            RegisterHotKey(this.Handle, 9015, 0, Keys.Scroll); 
            RegisterHotKey(this.Handle, 9016, 0, Keys.Pause);

            // 트레이 컨텍스트 메뉴 UI 설정

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
            
            //설치마법사 버전확인용 버전 
            var version = Assembly.GetExecutingAssembly().GetName().Version;
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


        //전역 핫키 처리 (WndProc)
        protected  override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32(); // 눌린 핫키 ID 확인
              
                if (id == 9011) //Num 요약
                {
                    _ = SumCaptureScreenAsync();
                }
                else if (id == 9012) // /(나누기) 전체
                {
                    _ = CaptureScreenAsync();
                }
                else if (id == 9013) // 곱하기(*) → PageUp
                {
                    keybd_event(VK_PRIOR, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    keybd_event(VK_PRIOR, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                else if (id == 9014) // 마이너스(-) → PageDown
                {
                    keybd_event(VK_NEXT, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    keybd_event(VK_NEXT, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                else if (id == 9015)
                {
                    FullTtsStop();
                    synth.Rate += 1;
                    if (synth.Rate > 9) synth.Rate = 9;
                    Console.WriteLine("TTS 속도 업:"+ synth.Rate);
                }
                else if (id == 9016)
                {
                    FullTtsStop();
                    synth.Rate -= 1;
                    if (synth.Rate < -9) synth.Rate = -9;
                    Console.WriteLine("TTS 속도 다운:" + synth.Rate);
                }
            }
            base.WndProc(ref m);
        }


        // 닫기 버튼 눌러도 종료되지 않도록 처리
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

      
        private string cachedApiToken = null;
        private SpeechSynthesizer synth = new SpeechSynthesizer();



        /*
         * 1. 화면 전체 캡처
         * 2. 날짜별 폴더 저장
         * 3. Base64 변환
         * 4. 서버 전송
         * 5. 1일 후 자동 삭제
        */

        //화면캡쳐 
        //전체
        public async Task CaptureScreenAsync()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            using (Bitmap bmp = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screen.Left, screen.Top, 0, 0, bmp.Size);
                }

                string todayFolder = Path.Combine(baseFolder, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(todayFolder))
                    Directory.CreateDirectory(todayFolder);

                string fileName = DateTime.Now.ToString("dd_HHmmss") + ".png";
                string fullPath = Path.Combine(todayFolder, fileName);
                bmp.Save(fullPath, ImageFormat.Png);

                // 파일별 삭제 타이머
                _ = DeleteFileAfterDelay(fullPath, TimeSpan.FromDays(1));

                // 1. Base64 변환
                string base64Image;
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    base64Image = Convert.ToBase64String(ms.ToArray());
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
                   await SendImageToServerAsync(base64Image, token, "read-main"); //전체
                }
                else
                {
                    MessageBox.Show("토큰 발급 실패로 이미지 전송이 취소되었습니다.");
                }
            }
        }

        //요약
        public async Task SumCaptureScreenAsync()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            using (Bitmap bmp = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screen.Left, screen.Top, 0, 0, bmp.Size);
                }

                string todayFolder = Path.Combine(baseFolder, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(todayFolder))
                    Directory.CreateDirectory(todayFolder);

                string fileName = DateTime.Now.ToString("dd_HHmmss") + ".png";
                string fullPath = Path.Combine(todayFolder, fileName);
                bmp.Save(fullPath, ImageFormat.Png);
                _ = DeleteFileAfterDelay(fullPath, TimeSpan.FromDays(1));

                // 1. Base64 변환
                string base64Image;
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    base64Image = Convert.ToBase64String(ms.ToArray());
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
                }
                else
                {
                    MessageBox.Show("토큰 발급 실패로 이미지 전송이 취소되었습니다.");
                }
            }
        }
        private void FullTtsStop()
        {
            int savedRate = synth.Rate;
            stopRequested = true;
            ttsQueue.Clear();       // 큐 비우기

            synth.SpeakAsyncCancelAll(); // 혹시 남아있을 요청 제거
            synth.Dispose();  // 엔진 완전 파괴
            synth = new SpeechSynthesizer(); // 새로 다시 생성

            synth.Rate = savedRate;
            synth.Volume = 100;

            isSpeaking = false;
            stopRequested = false;
        }


        // TTS 큐
        private readonly Queue<string> ttsQueue = new Queue<string>();
        private bool isSpeaking = false;
        private bool stopRequested = false;

        // 순차 음성 출력
        private async Task SpeakTextAsync(string text)
        {
            ttsQueue.Enqueue(text);

            if (isSpeaking)
                return;

            isSpeaking = true;

            while (ttsQueue.Count > 0)
            {
                if (stopRequested) break;

                string nextText = ttsQueue.Dequeue();
                var tcs = new TaskCompletionSource<bool>();

                EventHandler<SpeakCompletedEventArgs> handler = null;
                handler = (s, e) =>
                {
                    synth.SpeakCompleted -= handler;
                    tcs.TrySetResult(true);
                };

                synth.SpeakCompleted += handler;
                synth.SpeakAsync(nextText);

                await tcs.Task;
            }

            isSpeaking = false;
        }



        /*
         * - 토큰 없으면 자동 등록
         * - 스트리밍 응답 수신
         * - message 타입만 TTS 처리
        */
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
                    //provider = "openai"
                    provider = "google"
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await client.PostAsync(url, content))
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
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
                                await SpeakTextAsync(contentText);
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
     
        private async Task<string> RegisterClientAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var url = "http://222.109.31.211/api/v1/auth/register";

                    string serialKey = GetAvailableSerialKey();

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

        //토큰키 불러오는 위치
        private string tokenFilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
    "KEAD",
    "tokenkeys.txt"
);

        // 사용 가능한 키 중 랜덤 선택
        private string GetAvailableSerialKey()
        {
            if (!File.Exists(tokenFilePath))
                throw new FileNotFoundException("Token key 파일이 없습니다.", tokenFilePath);

            var lines = File.ReadAllLines(tokenFilePath).ToList();
            var availableKeys = new List<int>(); // 사용 가능한 키 인덱스 저장

            for (int i = 0; i < lines.Count; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length != 2) continue;

                if (int.TryParse(parts[1], out int count) && count < 3)
                    availableKeys.Add(i);
            }

            if (availableKeys.Count == 0)
                throw new Exception("사용 가능한 토큰 키가 없습니다.");

            // 랜덤 선택
            Random rnd = new Random();
            int selectedIndex = availableKeys[rnd.Next(availableKeys.Count)];

            // 사용 횟수 증가
            var selectedParts = lines[selectedIndex].Split('|');
            int currentCount = int.Parse(selectedParts[1]);
            lines[selectedIndex] = $"{selectedParts[0]}|{currentCount + 1}";

            // 파일에 업데이트
            File.WriteAllLines(tokenFilePath, lines);

            return selectedParts[0];
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
      
        // 폼이 닫힐때 발생하는 이벤트 : 백그라운드 실행
        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            if(e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void ContextMenuStrip_Exit(object sender, EventArgs e)
        {
            Application.Exit();
        }

            }
        }
