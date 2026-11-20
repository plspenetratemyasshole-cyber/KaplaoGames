#if UNITY_STANDALONE_WIN
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace KaplaoGames.Platform
{
    /// <summary>
    /// Native Windows open/save file dialogs (Explorer).
    /// </summary>
    public static class WindowsFileDialog
    {
        const int MaxPath = 260;
        const int FileBufferChars = 8192;

        const int OfnExplorer = 0x00080000;
        const int OfnFileMustExist = 0x00001000;
        const int OfnPathMustExist = 0x00000800;
        const int OfnOverwritePrompt = 0x00000002;
        const int OfnNoChangeDir = 0x00000008;

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetOpenFileNameW")]
        static extern bool GetOpenFileNameW(ref OpenFileName openFileName);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetSaveFileNameW")]
        static extern bool GetSaveFileNameW(ref OpenFileName openFileName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        public static string Open(string initialDirectory, string title)
        {
            return ShowDialog(initialDirectory, title, null, open: true, filter: JsonFilter, defaultExtension: null);
        }

        public static string OpenPng(string initialDirectory, string title)
        {
            return ShowDialog(initialDirectory, title, null, open: true, filter: PngFilter, defaultExtension: "png");
        }

        public static string OpenAudio(string initialDirectory, string title)
        {
            return ShowDialog(initialDirectory, title, null, open: true, filter: AudioFilter, defaultExtension: "ogg");
        }

        const string JsonFilter = "JSON Level (*.json)\0*.json\0All Files (*.*)\0*.*\0";
        const string PngFilter = "PNG Image (*.png)\0*.png\0All Files (*.*)\0*.*\0";
        const string AudioFilter = "Audio (*.ogg;*.mp3)\0*.ogg;*.mp3\0All Files (*.*)\0*.*\0";

        public static string Save(string initialDirectory, string title, string defaultFileName)
        {
            return ShowDialog(initialDirectory, title, defaultFileName, open: false, filter: JsonFilter, defaultExtension: "json");
        }

        static string ShowDialog(
            string initialDirectory,
            string title,
            string defaultFileName,
            bool open,
            string filter,
            string defaultExtension)
        {
            IntPtr fileBuffer = IntPtr.Zero;
            IntPtr fileTitleBuffer = IntPtr.Zero;

            try
            {
                fileBuffer = Marshal.AllocCoTaskMem(FileBufferChars * sizeof(char));
                fileTitleBuffer = Marshal.AllocCoTaskMem(MaxPath * sizeof(char));

                for (int i = 0; i < FileBufferChars; i++)
                    Marshal.WriteInt16(fileBuffer, i * sizeof(char), 0);

                if (!string.IsNullOrEmpty(defaultFileName))
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(defaultFileName);
                    int copyLength = Math.Min(bytes.Length, FileBufferChars * sizeof(char) - sizeof(char));
                    Marshal.Copy(bytes, 0, fileBuffer, copyLength);
                }

                IntPtr owner = GetActiveWindow();
                if (owner == IntPtr.Zero)
                    owner = Process.GetCurrentProcess().MainWindowHandle;

                var dialog = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = owner,
                    lpstrFilter = filter,
                    lpstrFile = fileBuffer,
                    nMaxFile = FileBufferChars,
                    lpstrFileTitle = fileTitleBuffer,
                    nMaxFileTitle = MaxPath,
                    lpstrInitialDir = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory,
                    lpstrTitle = title,
                    Flags = OfnExplorer | OfnNoChangeDir |
                            (open ? OfnFileMustExist | OfnPathMustExist : OfnPathMustExist | OfnOverwritePrompt),
                    lpstrDefExt = open ? null : defaultExtension
                };

                bool ok = open ? GetOpenFileNameW(ref dialog) : GetSaveFileNameW(ref dialog);
                if (!ok)
                    return null;

                return Marshal.PtrToStringUni(fileBuffer);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Kaplao] Windows file dialog failed: {ex.Message}");
                return null;
            }
            finally
            {
                if (fileBuffer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileBuffer);
                if (fileTitleBuffer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileTitleBuffer);
            }
        }
    }
}
#endif
