using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppTest2
{
    internal static class Program
    { // Win32 API

        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, Keys vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out Message msg, IntPtr hWnd, uint filterMin, uint filterMax, uint flags);

        private const int HOTKEY_ID = 9000;
        private const int WM_HOTKEY = 0x0312;
        private const int PM_REMOVE = 0x0001;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            SetProcessDPIAware(); // DPI 무시
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            // Form1 form = new Form1(); // 폼 생성
            //form.Show();              // 폼을 한 번 띄워서 핫키 등록 실행
            //Application.Run();
            //Form1 form = new Form1();
            //form.Hide(); // 처음엔 숨김 (트레이만 남김)
            // 전역 핫키 등록
            Form1 form = new Form1(); // 폼 생성 및 숨김

            form.WindowState = FormWindowState.Minimized;
            form.ShowInTaskbar = false;
            form.Opacity = 0;    // 완전히 안보이게
            form.Show();
            form.Hide();          // 트레이만 남기기


            RegisterHotKey(form.Handle, HOTKEY_ID, 0, Keys.F1);

            Thread hotkeyThread = new Thread(() =>
            {
                Message msg;
                while (true)
                {
                    if (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                    {
                        if (msg.Msg == WM_HOTKEY && msg.WParam.ToInt32() == HOTKEY_ID)
                        {
                            if (form != null && !form.IsDisposed)
                            {
                                try
                                {
                                    form.BeginInvoke((Action)(async () =>
                                    {
                                        await form.CaptureScreenAsync();
                                    }));
                                }
                                catch (ObjectDisposedException) { }
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
            });
            hotkeyThread.IsBackground = true;
            hotkeyThread.Start();

            Application.Run(form); // 메시지 루프 유지

            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        }

    }
}
