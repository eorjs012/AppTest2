using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppTest2
{  /// <summary>
   /// 애플리케이션 진입점
   /// - 백그라운드 실행
   /// - 전역 핫키 등록
   /// - 메시지 루프 유지
   /// </summary>
    internal static class Program
    {
        /* =========================================================
        * Win32 API 선언
        * ========================================================= */

        // 전역 핫키 등록
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, Keys vk);

        // 전역 핫키 해제
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 메시지 큐를 직접 조회 (WM_HOTKEY 감지용)
        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out Message msg, IntPtr hWnd, uint filterMin, uint filterMax, uint flags);


        /* =========================================================
        * 상수 정의
        * ========================================================= */
        private const int HOTKEY_ID = 9000;
        private const int WM_HOTKEY = 0x0312;
        private const int PM_REMOVE = 0x0001;


        // DPI 스케일링 무시 (고해상도 환경 대응)
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();


        private static Mutex _mutex;

        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;

            _mutex = new Mutex(
                true,
                "Global\\AppTest2_SingleInstance",
                out createdNew
            );
            if (!createdNew)
            {
                // 이미 실행 중이면 조용히 종료
                return;
            }

            /* -----------------------------------------------------
             * UI / DPI 기본 설정
             * ----------------------------------------------------- */

            SetProcessDPIAware();  // Windows DPI 자동 스케일링 비활성화
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            /* -----------------------------------------------------
           * 메인 폼 생성 (보이지 않게 실행)
           * ----------------------------------------------------- */

            // 실제 모든 로직을 담당하는 Form1 생성
            Form1 form = new Form1();


            // 사용자에게 보이지 않도록 설정
            form.WindowState = FormWindowState.Minimized;
            form.ShowInTaskbar = false;
            form.Opacity = 0;    // 완전히 안보이게
            form.Show();
            form.Hide();          // 트레이만 남기기


            /* -----------------------------------------------------
            * 전역 핫키 등록 (미니 키보드)
            * ----------------------------------------------------- */

            // 숫자 패드 0 ~ 9
            RegisterHotKey(form.Handle, 9001, 0, Keys.NumPad0);
            RegisterHotKey(form.Handle, 9002, 0, Keys.NumPad1);
            RegisterHotKey(form.Handle, 9003, 0, Keys.NumPad2);
            RegisterHotKey(form.Handle, 9004, 0, Keys.NumPad3);
            RegisterHotKey(form.Handle, 9005, 0, Keys.NumPad4);
            RegisterHotKey(form.Handle, 9006, 0, Keys.NumPad5);
            RegisterHotKey(form.Handle, 9007, 0, Keys.NumPad6);
            RegisterHotKey(form.Handle, 9008, 0, Keys.NumPad7);
            RegisterHotKey(form.Handle, 9009, 0, Keys.NumPad8);
            RegisterHotKey(form.Handle, 9010, 0, Keys.NumPad9);

            // 특수 키
            RegisterHotKey(form.Handle, 9011, 0, Keys.NumLock); 
            RegisterHotKey(form.Handle, 9012, 0, Keys.Divide); 
            RegisterHotKey(form.Handle, 9013, 0, Keys.Multiply);
            RegisterHotKey(form.Handle, 9014, 0, Keys.Subtract);
            RegisterHotKey(form.Handle, 9015, 0, Keys.Scroll);
            RegisterHotKey(form.Handle, 9016, 0, Keys.Pause);


            /* -----------------------------------------------------
           * 핫키 감지용 백그라운드 스레드
           * (※ 현재 Form.WndProc에서도 처리 중 → 중복 구조)
           * ----------------------------------------------------- */
            Thread hotkeyThread = new Thread(() =>
            {
                Message msg;
                while (true)
                {
                    // 메시지 큐에서 WM_HOTKEY 직접 확인
                    if (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                    {
                        if (msg.Msg == WM_HOTKEY && msg.WParam.ToInt32() == HOTKEY_ID)
                        {
                            // 폼이 살아있는 경우에만 실행
                            if (form != null && !form.IsDisposed)
                            {
                                try
                                {
                                    // UI 스레드에서 화면 캡처 실행
                                    form.BeginInvoke((Action)(async () =>
                                    {
                                        await form.CaptureScreenAsync();
                                    }));
                                }
                                catch (ObjectDisposedException) { }
                            }
                        }
                    }
                    Thread.Sleep(10);// CPU 점유 방지
                }
            });
            hotkeyThread.IsBackground = true;
            hotkeyThread.Start();

            /* -----------------------------------------------------
            * WinForms 메시지 루프 시작
            * ----------------------------------------------------- */
            Application.Run(form);


            /* -----------------------------------------------------
            * 종료 시 핫키 해제
            * ----------------------------------------------------- */

            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        }

    }
}
