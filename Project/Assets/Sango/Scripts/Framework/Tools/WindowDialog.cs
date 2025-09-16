using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class DialogConfig
{
    #region Config Field
    public int structSize = 0;//设置窗口大小
    public IntPtr dlgOwner = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public String filter = null;//文件类型
    public String customFilter = null;
    public int maxCustFilter = 0;
    public int filterIndex = 0;
    public IntPtr file = IntPtr.Zero;
    //public String file = null;
    public int maxFile = 0;
    public String fileTitle = null;
    public int maxFileTitle = 0;
    public String initialDir = null;//指定路劲
    public String title = null;//窗口名称
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public String defExt = null;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public String templateName = null;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;
    #endregion
}

#endif

public class WindowDialog
{
    public static string lastOpenDir;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
    public const int OFN_READONLY = 0x1;
    public const int OFN_OVERWRITEPROMPT = 0x2;
    public const int OFN_HIDEREADONLY = 0x4;
    public const int OFN_NOCHANGEDIR = 0x8;
    public const int OFN_SHOWHELP = 0x10;
    public const int OFN_ENABLEHOOK = 0x20;
    public const int OFN_ENABLETEMPLATE = 0x40;
    public const int OFN_ENABLETEMPLATEHANDLE = 0x80;
    public const int OFN_NOVALIDATE = 0x100;
    public const int OFN_ALLOWMULTISELECT = 0x200;
    public const int OFN_EXTENSIONDIFFERENT = 0x400;
    public const int OFN_PATHMUSTEXIST = 0x800;
    public const int OFN_FILEMUSTEXIST = 0x1000;
    public const int OFN_CREATEPROMPT = 0x2000;
    public const int OFN_SHAREAWARE = 0x4000;
    public const int OFN_NOREADONLYRETURN = 0x8000;
    public const int OFN_NOTESTFILECREATE = 0x10000;
    public const int OFN_NONETWORKBUTTON = 0x20000;
    public const int OFN_NOLONGNAMES = 0x40000;
    public const int OFN_EXPLORER = 0x80000;
    public const int OFN_NODEREFERENCELINKS = 0x100000;
    public const int OFN_LONGNAMES = 0x200000;
    public const int OFN_ENABLEINCLUDENOTIFY = 0x400000;
    public const int OFN_ENABLESIZING = 0x800000;
    public const int OFN_DONTADDTORECENT = 0x2000000;
    public const int OFN_FORCESHOWHIDDEN = 0x10000000;
    public const int OFN_EX_NOPLACESBAR = 0x1;
    public const int OFN_SHAREFALLTHROUGH = 2;
    public const int OFN_SHARENOWARN = 1;
    public const int OFN_SHAREWARN = 0;

    #region Win32API WRAP
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    static extern bool GetOpenFileName([In, Out] DialogConfig dialog);  //这个方法名称必须为GetOpenFileName
    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    static extern bool GetSaveFileName([In, Out] DialogConfig dialog);  //这个方法名称必须为GetSaveFileName
    #endregion
#endif

    /// <summary>
    /// 打开文件选择窗口
    /// </summary> 
    public static string[] OpenFileDialog(string filter, bool allowMultiSelect = false)
    {
        if(allowMultiSelect) 
            return OpenFileDialog("选择文件(单选)", null, filter, allowMultiSelect);
        else
            return OpenFileDialog("选择文件(可多选)", null, filter, allowMultiSelect);
    }

    /// <summary>
    /// 打开文件选择窗口
    /// </summary> 
    public static string[] OpenFileDialog(string title, string filter, bool allowMultiSelect = false)
    {
        return OpenFileDialog(title, null, filter, allowMultiSelect);
    }

    /// <summary>
    /// 打开文件选择窗口
    /// </summary> 
    public static string[] OpenFileDialog(string title, string initDir, string filter, bool allowMultiSelect = false)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (string.IsNullOrEmpty(lastOpenDir))
            lastOpenDir = Sango.Path.ContentRootPath;

        if (string.IsNullOrEmpty(initDir))
            initDir = lastOpenDir;

        const int MAX_FILE_LENGTH = 2048;

        DialogConfig ofn = new DialogConfig();

        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = filter.Replace("|", "\0") + "\0";
        ofn.fileTitle = new String(new char[MAX_FILE_LENGTH]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = initDir;
        ofn.title = title;
        ofn.flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;

        // Create buffer for file names
        string fileNames = new String(new char[MAX_FILE_LENGTH]);
        ofn.file = Marshal.StringToBSTR(fileNames);
        ofn.maxFile = fileNames.Length;

        if (allowMultiSelect) {
            ofn.flags |= OFN_ALLOWMULTISELECT;
        }

        if (GetOpenFileName(ofn)) {
            List<string> selectedFilesList = new List<string>();

            long pointer = (long)ofn.file;
            string file = Marshal.PtrToStringAuto(ofn.file);

            // Retrieve file names
            while (file.Length > 0) {
                selectedFilesList.Add(file);

                pointer += file.Length * 2 + 2;
                ofn.file = (IntPtr)pointer;
                file = Marshal.PtrToStringAuto(ofn.file);
            }

            if (selectedFilesList.Count == 1) {
                // Only one file selected with full path
                return selectedFilesList.ToArray();
            }
            else {
                // Multiple files selected, add directory
                string[] selectedFiles = new string[selectedFilesList.Count - 1];

                for (int i = 0; i < selectedFiles.Length; i++) {
                    selectedFiles[i] = selectedFilesList[0] + "\\" + selectedFilesList[i + 1];
                }

                return selectedFiles;
            }
        }
        else {
            // "Cancel" pressed
            return null;
        }

#else
        return null;
#endif
    }


    /// <summary>
    /// 保存文件选择窗口
    /// </summary>
    /// <param name="fileName">默认文件名字</param>
    /// <param name="filter">文件类型</param>
    /// <returns>文件路径</returns>
    public static string SaveFileDialog(string fileName, string filter)
    {
        return SaveFileDialog("保存", null, fileName, filter);
    }

    /// <summary>
    /// 保存文件选择窗口
    /// </summary>
    /// <param name="title">窗口名字</param>
    /// <param name="fileName">默认文件名字</param>
    /// <param name="filter">文件类型</param>
    /// <returns>文件路径</returns>
    public static string SaveFileDialog(string title, string fileName, string filter)
    {
        return SaveFileDialog(title, null, fileName, filter);
    }

    /// <summary>
    /// 保存文件选择窗口
    /// </summary>
    /// <param name="title">指定窗口名称</param>
    /// <param name="extensions">预设文件存储位置及文件名</param>
    /// <returns>文件路径</returns>
    public static string SaveFileDialog(string title, string initDir, string fileName, string filter)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (string.IsNullOrEmpty(lastOpenDir))
            lastOpenDir = Sango.Path.ContentRootPath;

        if (string.IsNullOrEmpty(initDir))
            initDir = lastOpenDir;

        DialogConfig ofn = new DialogConfig();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = filter.Replace("|", "\0") + "\0";
        ofn.filterIndex = 0;
        var chars = new char[256];
        var it = Path.GetFileName(fileName).GetEnumerator();
        for (int i = 0; i < chars.Length && it.MoveNext(); ++i) {
            chars[i] = it.Current;
        }
        string fileNames = new string(chars);
        ofn.file = Marshal.StringToBSTR(fileNames); ;
        ofn.maxFile = fileNames.Length;
        ofn.fileTitle = new string(new char[64]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = initDir;
        ofn.title = title;
        ofn.flags = OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY | OFN_NOCHANGEDIR;
        ofn.dlgOwner = GetForegroundWindow(); //这一步将文件选择窗口置顶。
        if (!GetSaveFileName(ofn)) {
            return null;
        }

        string file = Marshal.PtrToStringAuto(ofn.file);
        return file;
#else
        return null;
#endif
    }
}
